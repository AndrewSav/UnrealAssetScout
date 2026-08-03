using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CUE4Parse.MappingsProvider;

namespace UnrealAssetScout.Incremental;

// Reduces a loaded usmap to a semantic fingerprint per type and per enum, plus the reference graph
// those fingerprints are looked up through.
// Called by IncrementalRunner during PLAN with the provider's MappingsForGame, and the result is
// both diffed against the manifest's usmap block and written back into the new manifest.
// Fingerprints are semantic, not byte based, so a regenerated usmap for an unchanged game compares
// equal despite name-table reordering. Only a type's own declared members are hashed: the Super
// chain is never walked, both because each chain level is fingerprinted separately anyway and
// because CUE4Parse's Struct.Super has no cycle guard.
internal static class UsmapFingerprints
{
    internal static UsmapSnapshot From(TypeMappings? mappings)
    {
        if (mappings is null)
            return UsmapSnapshot.Empty;

        // Propagates CUE4Parse's own Types comparer rather than hardcoding OrdinalIgnoreCase, so
        // this keeps tracking CUE4Parse if that choice ever changes.
        var typeFingerprints = new Dictionary<string, string>(mappings.Types.Count, mappings.Types.Comparer);
        var nodes = new Dictionary<string, UsmapTypeNode>(mappings.Types.Count, mappings.Types.Comparer);

        foreach (var (name, type) in mappings.Types)
        {
            var referencedTypes = new SortedSet<string>(StringComparer.Ordinal);
            var referencedEnums = new SortedSet<string>(StringComparer.Ordinal);
            var builder = new StringBuilder();

            builder.Append(name).Append('|').Append(type.SuperType ?? string.Empty);

            foreach (var (index, property) in type.Properties.OrderBy(pair => pair.Key))
            {
                builder.Append('|').Append(index).Append(':').Append(property.Name).Append(':');
                AppendDescriptor(builder, property.MappingType, referencedTypes, referencedEnums);
            }

            typeFingerprints[name] = Hash(builder.ToString());
            nodes[name] = new UsmapTypeNode(name, type.SuperType, [.. referencedTypes], [.. referencedEnums]);
        }

        // Enums default to a case-sensitive comparer, unlike Types; propagated the same way.
        var enumFingerprints = mappings.Enums.ToDictionary(
            pair => pair.Key,
            pair => Hash(pair.Key + "|" + string.Join(
                ',', pair.Value.OrderBy(member => member.Key).Select(member => $"{member.Key}={member.Value}"))),
            mappings.Enums.Comparer);

        return new UsmapSnapshot
        {
            TypeFingerprints = typeFingerprints,
            EnumFingerprints = enumFingerprints,
            Types = nodes
        };
    }

    private static void AppendDescriptor(
        StringBuilder builder, PropertyType? type, SortedSet<string> types, SortedSet<string> enums)
    {
        if (type is null)
        {
            builder.Append('-');
            return;
        }

        builder.Append(type.Type);

        if (type.StructType is { Length: > 0 } structType)
        {
            builder.Append('<').Append(structType).Append('>');
            types.Add(structType);
        }

        if (type.EnumName is { Length: > 0 } enumName)
        {
            builder.Append('#').Append(enumName);
            enums.Add(enumName);
        }

        if (type.InnerType is not null)
        {
            builder.Append('[');
            AppendDescriptor(builder, type.InnerType, types, enums);
            builder.Append(']');
        }

        if (type.ValueType is not null)
        {
            builder.Append('{');
            AppendDescriptor(builder, type.ValueType, types, enums);
            builder.Append('}');
        }
    }

    private static string Hash(string canonical) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
}
