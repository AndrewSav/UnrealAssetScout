using System.Collections.Generic;
using System.Linq;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.FileProvider.Vfs;
using CUE4Parse.UE4.IO;
using CUE4Parse.UE4.IO.Objects;
using CUE4Parse.UE4.Pak;
using CUE4Parse.UE4.Pak.Objects;
using CUE4Parse.UE4.Readers;
using UnrealAssetScout.Logging;
using UnrealAssetScout.Utils;

namespace UnrealAssetScout.Incremental;

// Every mounted container entry's path mapped to the fingerprint its packer already stored.
// Built once by IncrementalRunner at the start of PLAN and handed to ExportPlanner as plain data.
// Pak entries are read in one batch by PakInlineHeaderBatchReader, IoStore entries from each
// container's table of contents.
// Fingerprinting is blanket rather than selective: any path we might later need is then covered
// with no rule about which paths qualify, and only referenced paths are persisted into the
// manifest. Entries whose container stores no usable hash are counted, not silently dropped.
internal sealed class SourceFingerprintIndex
{
    internal required IReadOnlyDictionary<string, string> ByPath { get; init; }
    internal required int UnfingerprintedCount { get; init; }

    internal static SourceFingerprintIndex Build(AbstractVfsFileProvider provider)
    {
        var files = ProviderFiles.Resolved(provider).ToList();
        var pakFingerprints = ReadPakFingerprints(files);
        var ioStoreHashes = new Dictionary<IoStoreReader, IReadOnlyDictionary<FIoChunkId, string>>();

        return FromEntries(files.Select(file => (file.Path, Fingerprint: file switch
        {
            FPakEntry pakEntry => pakFingerprints.GetValueOrDefault(pakEntry),
            FIoStoreEntry ioEntry => IoStoreFingerprint(ioEntry, ioStoreHashes),
            _ => null
        })));
    }

    // A pak mounted from a stream a caller supplied may not hold the file's bytes, so only a pak
    // CUE4Parse reads straight from a file is read by path; any other goes through its archive.
    private static Dictionary<FPakEntry, string?> ReadPakFingerprints(IReadOnlyList<GameFile> files)
    {
        var entries = files.OfType<FPakEntry>().ToList();
        var requests = entries.Select(entry => entry.Vfs is PakFileReader { Ar: FRandomAccessFileStreamArchive } reader
            ? new PakInlineHeaderRequest(reader.Path, entry.Offset, entry.CompressedSize, entry.UncompressedSize,
                PakInlineHeaderLayout.HashOffset(reader.Info.Version, reader.Info.IsSubVersion))
            : new PakInlineHeaderRequest(null, 0, 0, 0, 0)).ToList();

        var fingerprints = PakInlineHeaderBatchReader.ReadFingerprints(requests,
            index => PakInlineHeaderFingerprints.TryRead(entries[index], out var hash) ? hash : null);

        var byEntry = new Dictionary<FPakEntry, string?>(entries.Count, ReferenceEqualityComparer.Instance);
        for (var index = 0; index < entries.Count; index++)
            byEntry[entries[index]] = fingerprints[index];

        return byEntry;
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
