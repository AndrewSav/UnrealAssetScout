#!/usr/bin/env python3
"""
usmap2json.py - dump an Unreal Engine .usmap (unversioned property mappings)
file to JSON or a flat text listing.

Supports usmap versions 0-4 (Initial, PackageVersioning, LongFName, LargeEnums,
ExplicitEnumValues) and None / Oodle / Brotli / Zstandard compression.

Usage:
    python usmap2json.py Mappings.usmap                 -> Mappings.json
    python usmap2json.py Mappings.usmap out.json
    python usmap2json.py Mappings.usmap --txt           -> Mappings.txt

Optional deps (only if your file uses that compression):
    brotli      -> pip install brotli
    zstandard   -> pip install zstandard
    oodle       -> put oo2core_9_win64.dll next to the script or in the cwd
"""

import io
import json
import os
import struct
import sys

MAGIC = 0x30C4

V_INITIAL, V_PACKAGE_VERSIONING, V_LONG_FNAME, V_LARGE_ENUMS, V_EXPLICIT_ENUM_VALUES = range(5)
V_LATEST = V_EXPLICIT_ENUM_VALUES

COMPRESSION = {0: "None", 1: "Oodle", 2: "Brotli", 3: "Zstandard"}

PROPERTY_TYPES = [
    "ByteProperty", "BoolProperty", "IntProperty", "FloatProperty",
    "ObjectProperty", "NameProperty", "DelegateProperty", "DoubleProperty",
    "ArrayProperty", "StructProperty", "StrProperty", "TextProperty",
    "InterfaceProperty", "MulticastDelegateProperty", "WeakObjectProperty",
    "LazyObjectProperty", "AssetObjectProperty", "SoftObjectProperty",
    "UInt64Property", "UInt32Property", "UInt16Property", "Int64Property",
    "Int16Property", "Int8Property", "MapProperty", "SetProperty",
    "EnumProperty", "FieldPathProperty", "OptionalProperty",
    "Utf8StrProperty", "AnsiStrProperty",
]

PRETTY_LEAF = {
    "ByteProperty": "uint8", "BoolProperty": "bool", "IntProperty": "int32",
    "FloatProperty": "float", "DoubleProperty": "double", "NameProperty": "FName",
    "StrProperty": "FString", "TextProperty": "FText", "ObjectProperty": "UObject*",
    "UInt64Property": "uint64", "UInt32Property": "uint32", "UInt16Property": "uint16",
    "Int64Property": "int64", "Int16Property": "int16", "Int8Property": "int8",
    "SoftObjectProperty": "TSoftObjectPtr", "WeakObjectProperty": "TWeakObjectPtr",
    "LazyObjectProperty": "TLazyObjectPtr", "AssetObjectProperty": "FSoftObjectPath",
    "InterfaceProperty": "TScriptInterface", "DelegateProperty": "FScriptDelegate",
    "MulticastDelegateProperty": "FMulticastScriptDelegate",
    "FieldPathProperty": "TFieldPath", "Utf8StrProperty": "FUtf8String",
    "AnsiStrProperty": "FAnsiString",
}


class Reader:
    def __init__(self, data):
        self.d = data
        self.p = 0

    def take(self, n):
        if self.p + n > len(self.d):
            raise EOFError("unexpected end of usmap data")
        b = self.d[self.p:self.p + n]
        self.p += n
        return b

    def u8(self):
        return self.take(1)[0]

    def u16(self):
        return struct.unpack("<H", self.take(2))[0]

    def i32(self):
        return struct.unpack("<i", self.take(4))[0]

    def u32(self):
        return struct.unpack("<I", self.take(4))[0]

    def i64(self):
        return struct.unpack("<q", self.take(8))[0]

    def name(self, lut):
        idx = self.i32()
        if idx < 0 or idx >= len(lut):
            return None
        return lut[idx]


def oodle_decompress(comp, size):
    import ctypes
    import glob
    cands = []
    for d in (os.getcwd(), os.path.dirname(os.path.abspath(__file__))):
        cands += sorted(glob.glob(os.path.join(d, "oo2core_*.dll")))
    if not cands:
        raise RuntimeError(
            "This usmap is Oodle-compressed. Copy oo2core_9_win64.dll (found in any UE "
            "game's Binaries/Win64 folder) into this directory and re-run.")
    lib = ctypes.windll.LoadLibrary(cands[0])
    fn = lib.OodleLZ_Decompress
    fn.restype = ctypes.c_int64
    out = ctypes.create_string_buffer(size)
    n = fn(ctypes.c_char_p(comp), ctypes.c_int64(len(comp)), out, ctypes.c_int64(size),
           0, 0, 0, 0, 0, 0, 0, 0, 0, 3)
    if n <= 0:
        raise RuntimeError("Oodle decompression failed")
    return out.raw[:size]


def decompress(method, comp, size):
    if method == 0:
        return comp
    if method == 1:
        return oodle_decompress(comp, size)
    if method == 2:
        try:
            import brotli
        except ImportError:
            raise RuntimeError("This usmap is Brotli-compressed: pip install brotli")
        return brotli.decompress(comp)
    if method == 3:
        try:
            import zstandard
        except ImportError:
            raise RuntimeError("This usmap is Zstd-compressed: pip install zstandard")
        return zstandard.ZstdDecompressor().decompress(comp, max_output_size=size)
    raise RuntimeError(f"unknown compression method {method}")


def read_type(r, lut, version):
    t = r.u8()
    name = PROPERTY_TYPES[t] if t < len(PROPERTY_TYPES) else f"Unknown({t})"
    node = {"type": name}
    if name == "EnumProperty":
        node["inner"] = read_type(r, lut, version)
        node["enumName"] = r.name(lut)
    elif name == "StructProperty":
        node["structName"] = r.name(lut)
    elif name in ("ArrayProperty", "SetProperty", "OptionalProperty"):
        node["inner"] = read_type(r, lut, version)
    elif name == "MapProperty":
        node["inner"] = read_type(r, lut, version)
        node["value"] = read_type(r, lut, version)
    return node


def pretty(node):
    t = node["type"]
    if t == "StructProperty":
        return node.get("structName") or "struct?"
    if t == "EnumProperty":
        return node.get("enumName") or "enum?"
    if t == "ArrayProperty":
        return f"TArray<{pretty(node['inner'])}>"
    if t == "SetProperty":
        return f"TSet<{pretty(node['inner'])}>"
    if t == "OptionalProperty":
        return f"TOptional<{pretty(node['inner'])}>"
    if t == "MapProperty":
        return f"TMap<{pretty(node['inner'])}, {pretty(node['value'])}>"
    return PRETTY_LEAF.get(t, t)


def parse(path):
    raw = open(path, "rb").read()
    r = Reader(raw)

    if r.u16() != MAGIC:
        raise RuntimeError("not a .usmap file (bad magic)")
    version = r.u8()
    if version > V_LATEST:
        raise RuntimeError(
            f"usmap version {version} is newer than this script understands "
            f"(max {V_LATEST}); the format has probably been extended again")

    meta = {"usmapVersion": version}
    if version >= V_PACKAGE_VERSIONING and r.i32() > 0:
        meta["fileVersionUE4"] = r.i32()
        meta["fileVersionUE5"] = r.i32()
        custom = []
        for _ in range(r.i32()):
            guid = r.take(16).hex()
            custom.append({"guid": guid, "version": r.i32()})
        meta["customVersions"] = custom
        meta["netCL"] = r.u32()

    method = r.u8()
    meta["compression"] = COMPRESSION.get(method, method)
    comp_size, decomp_size = r.u32(), r.u32()
    body = decompress(method, r.take(comp_size), decomp_size)
    if len(body) != decomp_size:
        raise RuntimeError(f"decompressed {len(body)} bytes, expected {decomp_size}")

    r = Reader(body)

    lut = []
    for _ in range(r.u32()):
        n = r.u16() if version >= V_LONG_FNAME else r.u8()
        lut.append(r.take(n).decode("utf-8", "replace"))

    enums = {}
    for _ in range(r.u32()):
        enum_name = r.name(lut)
        count = r.u16() if version >= V_LARGE_ENUMS else r.u8()
        values = {}
        if version >= V_EXPLICIT_ENUM_VALUES:
            for _ in range(count):
                v = r.i64()
                values[str(v)] = r.name(lut)
        else:
            for j in range(count):
                values[str(j)] = r.name(lut)
        enums.setdefault(enum_name, values)

    structs = {}
    for _ in range(r.u32()):
        s_name = r.name(lut)
        s_super = r.name(lut)
        prop_count = r.u16()
        serializable = r.u16()
        props = []
        for _ in range(serializable):
            idx = r.u16()
            array_size = r.u8()
            p_name = r.name(lut)
            node = read_type(r, lut, version)
            props.append({
                "index": idx,
                "arraySize": array_size,
                "name": p_name,
                "type": pretty(node),
                "raw": node,
            })
        structs[s_name] = {
            "super": s_super,
            "propertyCount": prop_count,
            "properties": props,
        }

    meta["nameCount"] = len(lut)
    meta["enumCount"] = len(enums)
    meta["structCount"] = len(structs)
    meta["trailingBytes"] = len(body) - r.p
    return {"meta": meta, "enums": enums, "structs": structs}


def to_text(m):
    out = []
    meta = m["meta"]
    out.append(f"# usmap v{meta['usmapVersion']}  compression={meta['compression']}  "
               f"names={meta['nameCount']}  enums={meta['enumCount']}  structs={meta['structCount']}")
    out.append("")
    out.append("=== ENUMS ===")
    for name, values in sorted(m["enums"].items(), key=lambda kv: (kv[0] or "")):
        out.append(f"\nenum {name}")
        for v, n in sorted(values.items(), key=lambda kv: int(kv[0])):
            out.append(f"    {n} = {v}")
    out.append("")
    out.append("=== STRUCTS / CLASSES ===")
    for name, s in sorted(m["structs"].items(), key=lambda kv: (kv[0] or "")):
        head = f"\n{name}"
        if s["super"]:
            head += f" : {s['super']}"
        out.append(f"{head}   ({s['propertyCount']} properties)")
        for p in s["properties"]:
            dim = f"[{p['arraySize']}]" if p["arraySize"] > 1 else ""
            out.append(f"    [{p['index']:>4}] {p['type']} {p['name']}{dim}")
    return "\n".join(out) + "\n"


def main(argv):
    args = [a for a in argv[1:] if not a.startswith("-")]
    flags = {a for a in argv[1:] if a.startswith("-")}
    if not args:
        print(__doc__)
        return 1
    src = args[0]
    as_text = "--txt" in flags or "-t" in flags
    dst = args[1] if len(args) > 1 else os.path.splitext(src)[0] + (".txt" if as_text else ".json")

    m = parse(src)
    with open(dst, "w", encoding="utf-8") as f:
        if as_text:
            f.write(to_text(m))
        else:
            json.dump(m, f, indent=2, ensure_ascii=False)

    meta = m["meta"]
    print(f"{src}: usmap v{meta['usmapVersion']}, {meta['compression']} compression, "
          f"{meta['enumCount']} enums, {meta['structCount']} structs -> {dst}")
    if meta["trailingBytes"]:
        print(f"note: {meta['trailingBytes']} trailing bytes ignored (usmap extension blocks)")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
