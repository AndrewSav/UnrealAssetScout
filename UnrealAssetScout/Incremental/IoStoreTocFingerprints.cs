using System;
using System.Collections.Generic;
using System.IO;
using CUE4Parse.UE4.IO;
using CUE4Parse.UE4.IO.Objects;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Readers;

namespace UnrealAssetScout.Incremental;

// Reads the per-chunk hashes an IoStore container already records in its .utoc.
// Called by SourceFingerprintIndex once per mounted IoStore container during PLAN.
// The provider mounts with ReadDirectoryIndex only, so ChunkMetas is null on the live reader and
// the .utoc has to be re-read with ReadTocMeta, the same way IoStoreReader loads its own toc bytes.
internal static class IoStoreTocFingerprints
{
    internal static IReadOnlyDictionary<FIoChunkId, string> ReadChunkHashes(IoStoreReader reader)
    {
        var tocBytes = File.ReadAllBytes(reader.Path);
        using var tocArchive = new FByteArchive(reader.Path, tocBytes, reader.Versions);
        var toc = new FIoStoreTocResource(tocArchive, EIoStoreTocReadOptions.ReadTocMeta);

        var hashes = new Dictionary<FIoChunkId, string>(toc.ChunkIds.Length);
        if (toc.ChunkMetas is not { } metas)
            return hashes;

        for (var i = 0; i < toc.ChunkIds.Length && i < metas.Length; i++)
        {
            if (ExtractHash(metas[i].ChunkHash) is { } hash)
                hashes[toc.ChunkIds[i]] = hash;
        }

        return hashes;
    }

    // Mirrors PakInlineHeaderFingerprints.ExtractHash's own guard: an all-zero hash means the
    // packer wrote no hash for this chunk, not a real fingerprint that happens to be zero. Treating
    // it as one would give every such chunk the same stable, valid-looking fingerprint, so nothing
    // in it would ever compare changed. FSHAHash.IsValid(), despite the name, is exactly that
    // all-zero test.
    internal static string? ExtractHash(FSHAHash hash) =>
        hash.IsValid() ? null : Convert.ToBase64String(hash.Hash);
}
