using CUE4Parse.UE4.Pak.Objects;

namespace UnrealAssetScout.Incremental;

// Where the packer's stored SHA-1 sits inside the FPakEntry copy written inline before every file.
// Used by PakInlineHeaderFingerprints. Derived from the layout FPakEntry's own parser reads, so it
// stays correct for older paks that write a byte-wide compression index or an 8-byte timestamp.
internal static class PakInlineHeaderLayout
{
    internal const int HashSize = 20;

    // Offset 8 + CompressedSize 8 + UncompressedSize 8 = 24 bytes before the compression method.
    private const int FixedPrefix = 24;

    internal static int HashOffset(EPakFileVersion version, bool isSubVersion)
    {
        var compressionMethodSize =
            version == EPakFileVersion.PakFile_Version_FNameBasedCompressionMethod && !isSubVersion ? 1 : 4;
        var timestampSize = version < EPakFileVersion.PakFile_Version_NoTimestamps ? 8 : 0;

        return FixedPrefix + compressionMethodSize + timestampSize;
    }
}
