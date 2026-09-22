using System;
using CUE4Parse.UE4.Pak;
using CUE4Parse.UE4.Pak.Objects;

namespace UnrealAssetScout.Incremental;

// Interprets a pak entry's inline header, where the packer stored the entry's hash, for
// PakInlineHeaderBatchReader. TryRead reads a single entry through CUE4Parse's own archive, which
// SourceFingerprintIndex uses as the fallback for a pak that is not a plain file on disk.
internal static class PakInlineHeaderFingerprints
{
    internal static bool TryRead(FPakEntry entry, out string fingerprint)
    {
        fingerprint = string.Empty;
        if (entry.Vfs is not PakFileReader reader)
            return false;

        var hashOffset = PakInlineHeaderLayout.HashOffset(reader.Info.Version, reader.Info.IsSubVersion);

        byte[] header;
        try
        {
            header = reader.Ar.ReadBytesAt(entry.Offset, hashOffset + PakInlineHeaderLayout.HashSize);
        }
        catch (Exception)
        {
            return false;
        }

        if (!LayoutMatches(header, entry.CompressedSize, entry.UncompressedSize))
            return false;

        var hash = ExtractHash(header, hashOffset);
        if (hash is null)
            return false;

        fingerprint = hash;
        return true;
    }

    // The inline copy repeats the entry's sizes. If they do not match, the offset is wrong for
    // this pak and no fingerprint is safe to derive from it.
    internal static bool LayoutMatches(ReadOnlySpan<byte> header, long compressedSize, long uncompressedSize) =>
        BitConverter.ToInt64(header[8..16]) == compressedSize &&
        BitConverter.ToInt64(header[16..24]) == uncompressedSize;

    internal static string? ExtractHash(ReadOnlySpan<byte> header, int hashOffset)
    {
        var hash = header.Slice(hashOffset, PakInlineHeaderLayout.HashSize);

        // All-zero bytes mean the packer wrote no hash for this entry, not a real fingerprint
        // that happens to be zero.
        return hash.IndexOfAnyExcept((byte) 0) < 0 ? null : Convert.ToBase64String(hash);
    }
}
