using System;
using CUE4Parse.UE4.Pak;
using CUE4Parse.UE4.Pak.Objects;

namespace UnrealAssetScout.Incremental;

// Reads the fingerprint of a pak entry from the inline header the packer already wrote, rather
// than hashing the content. Called by SourceFingerprintIndex once per mounted pak entry during
// PLAN. Every read is validated against the entry's own sizes, because a wrong offset would
// produce plausible-looking but meaningless hashes and silently skip real work.
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

        if (!LayoutMatches(header, entry))
            return false;

        var hash = ExtractHash(header, hashOffset);
        if (hash is null)
            return false;

        fingerprint = hash;
        return true;
    }

    // The inline copy repeats the entry's sizes. If they do not match, the offset is wrong for
    // this pak and no fingerprint is safe to derive from it.
    private static bool LayoutMatches(ReadOnlySpan<byte> header, FPakEntry entry) =>
        BitConverter.ToInt64(header[8..16]) == entry.CompressedSize &&
        BitConverter.ToInt64(header[16..24]) == entry.UncompressedSize;

    internal static string? ExtractHash(ReadOnlySpan<byte> header, int hashOffset)
    {
        var hash = header.Slice(hashOffset, PakInlineHeaderLayout.HashSize);

        // All-zero bytes mean the packer wrote no hash for this entry, not a real fingerprint
        // that happens to be zero.
        return hash.IndexOfAnyExcept((byte) 0) < 0 ? null : Convert.ToBase64String(hash);
    }
}
