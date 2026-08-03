using CUE4Parse.UE4.Pak.Objects;
using UnrealAssetScout.Incremental;

namespace UnrealAssetScout.Tests;

public sealed class PakInlineHeaderLayoutTests
{
    [Fact]
    public void HashOffset_ModernPak_IsTwentyEight()
    {
        // Offset 8 + CompressedSize 8 + UncompressedSize 8 + int compression index 4.
        var offset = PakInlineHeaderLayout.HashOffset(
            EPakFileVersion.PakFile_Version_FrozenIndex, isSubVersion: false);

        Assert.Equal(28, offset);
    }

    [Fact]
    public void HashOffset_ByteWideCompressionIndex_IsTwentyFive()
    {
        // The one version that writes the compression method as a single byte.
        var offset = PakInlineHeaderLayout.HashOffset(
            EPakFileVersion.PakFile_Version_FNameBasedCompressionMethod, isSubVersion: false);

        Assert.Equal(25, offset);
    }

    [Fact]
    public void HashOffset_SubVersionOfTheSameVersion_IsTwentyEight()
    {
        var offset = PakInlineHeaderLayout.HashOffset(
            EPakFileVersion.PakFile_Version_FNameBasedCompressionMethod, isSubVersion: true);

        Assert.Equal(28, offset);
    }

    [Fact]
    public void HashOffset_PakWithTimestamps_AddsEightBytes()
    {
        // Versions below NoTimestamps write an 8-byte timestamp before the hash.
        var offset = PakInlineHeaderLayout.HashOffset(
            EPakFileVersion.PakFile_Version_Initial, isSubVersion: false);

        Assert.Equal(36, offset);
    }

    [Fact]
    public void HashOffset_ExactlyAtNoTimestamps_HasNoTimestamp()
    {
        // NoTimestamps itself is the version that dropped the timestamp field, so the comparison
        // must be strictly less-than: at this exact version there is no timestamp to skip.
        var offset = PakInlineHeaderLayout.HashOffset(
            EPakFileVersion.PakFile_Version_NoTimestamps, isSubVersion: false);

        Assert.Equal(28, offset);
    }

    [Fact]
    public void ExtractHash_ReturnsBase64OfTheTwentyStoredBytes()
    {
        var header = new byte[PakInlineHeaderLayout.HashOffset(EPakFileVersion.PakFile_Version_FrozenIndex, false)
                               + PakInlineHeaderLayout.HashSize];
        for (var i = 0; i < PakInlineHeaderLayout.HashSize; i++)
            header[28 + i] = (byte) (i + 1);

        var fingerprint = PakInlineHeaderFingerprints.ExtractHash(header, 28);

        Assert.Equal(Convert.ToBase64String(Enumerable.Range(1, 20).Select(i => (byte) i).ToArray()), fingerprint);
    }

    [Fact]
    public void ExtractHash_AllZeroHash_ReturnsNull()
    {
        // An all-zero hash means the packer did not store one. Treating it as a real fingerprint
        // would make every such entry compare equal to every other, which silently skips work.
        var header = new byte[48];

        Assert.Null(PakInlineHeaderFingerprints.ExtractHash(header, 28));
    }
}
