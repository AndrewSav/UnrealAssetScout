using System.Collections.Generic;
using System.Linq;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.FileProvider.Vfs;
using CUE4Parse.UE4.IO;
using CUE4Parse.UE4.IO.Objects;
using CUE4Parse.UE4.Pak.Objects;
using UnrealAssetScout.Logging;

namespace UnrealAssetScout.Incremental;

// Every mounted container entry's path mapped to the fingerprint its packer already stored.
// Built once by IncrementalRunner at the start of PLAN and handed to ExportPlanner as plain data.
// Fingerprinting is blanket rather than selective: any path we might later need is then covered
// with no rule about which paths qualify, and only referenced paths are persisted into the
// manifest. Entries whose container stores no usable hash are counted, not silently dropped.
internal sealed class SourceFingerprintIndex
{
    internal required IReadOnlyDictionary<string, string> ByPath { get; init; }
    internal required int UnfingerprintedCount { get; init; }

    internal static SourceFingerprintIndex Build(AbstractVfsFileProvider provider)
    {
        var ioStoreHashes = new Dictionary<IoStoreReader, IReadOnlyDictionary<FIoChunkId, string>>();

        return FromEntries(ResolvedFiles(provider).Select(file => (file.Path, Fingerprint: file switch
        {
            FPakEntry pakEntry => PakInlineHeaderFingerprints.TryRead(pakEntry, out var hash) ? hash : null,
            FIoStoreEntry ioEntry => IoStoreFingerprint(ioEntry, ioStoreHashes),
            _ => null
        })));
    }

    // FileProviderDictionary.Keys and Values both enumerate every mounted container's own path set
    // in turn, highest read order first, so a path shadowed by a patch container is yielded once
    // per container that mounts it, patch before base.
    internal static IEnumerable<GameFile> ResolvedFiles(AbstractVfsFileProvider provider)
    {
        foreach (var path in new HashSet<string>(provider.Files.Keys, provider.PathComparer))
            yield return provider.Files[path];
    }

    internal static SourceFingerprintIndex FromEntries(IEnumerable<(string Path, string? Fingerprint)> entries)
    {
        // OrdinalIgnoreCase to match the provider, which this application always configures with
        // StringComparer.OrdinalIgnoreCase. IncrementalRunner.ResolvePackagePath's FilesById leg
        // resolves a "packageid:" identity through the provider's own by-id index, which is not
        // guaranteed to hand back a path in the exact same casing this index was built with. A
        // stricter comparer here would lose the lookup rather than merely mismatch it, and the
        // dependency's fingerprint would never be recorded under its identity: permanently
        // under-invalidating that dependency instead of the safe over-invalidating direction.
        var byPath = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        var unfingerprinted = 0;

        foreach (var (path, fingerprint) in entries)
        {
            if (fingerprint is null)
            {
                unfingerprinted++;
                continue;
            }

            byPath[path] = fingerprint;
        }

        if (unfingerprinted > 0)
        {
            AppLog.Warning(
                "{Count} container entries have no stored fingerprint and will be re-exported every run",
                unfingerprinted);
        }

        return new SourceFingerprintIndex { ByPath = byPath, UnfingerprintedCount = unfingerprinted };
    }

    private static string? IoStoreFingerprint(
        FIoStoreEntry entry, Dictionary<IoStoreReader, IReadOnlyDictionary<FIoChunkId, string>> cache)
    {
        if (entry.Vfs is not IoStoreReader reader)
            return null;

        if (!cache.TryGetValue(reader, out var hashes))
            cache[reader] = hashes = IoStoreTocFingerprints.ReadChunkHashes(reader);

        return hashes.GetValueOrDefault(entry.ChunkId);
    }
}
