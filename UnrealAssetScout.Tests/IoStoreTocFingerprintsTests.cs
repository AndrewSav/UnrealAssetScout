using CUE4Parse.UE4.Objects.Core.Misc;
using UnrealAssetScout.Incremental;

namespace UnrealAssetScout.Tests;

public sealed class IoStoreTocFingerprintsTests
{
    [Fact]
    public void ExtractHash_ReturnsBase64OfTheTwentyStoredBytes()
    {
        var bytes = Enumerable.Range(1, 20).Select(i => (byte) i).ToArray();

        var fingerprint = IoStoreTocFingerprints.ExtractHash(new FSHAHash(bytes));

        Assert.Equal(Convert.ToBase64String(bytes), fingerprint);
    }

    [Fact]
    public void ExtractHash_AllZeroHash_ReturnsNull()
    {
        // An all-zero hash means the packer did not store one for this chunk. Treating it as a
        // real fingerprint would give every such chunk the same stable, valid-looking value, so
        // nothing in it would ever compare changed -- silent, permanent under-invalidation.
        var fingerprint = IoStoreTocFingerprints.ExtractHash(new FSHAHash(new byte[20]));

        Assert.Null(fingerprint);
    }
}
