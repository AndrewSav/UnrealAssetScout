using UnrealAssetScout.Incremental;

namespace UnrealAssetScout.Tests;

public sealed class SourceFingerprintIndexTests
{
    [Fact]
    public void FromEntries_MapsEveryPathToItsHash()
    {
        var index = SourceFingerprintIndex.FromEntries(
        [
            ("Game/A.uasset", "aaaa"),
            ("Game/A.uexp", "bbbb")
        ]);

        Assert.Equal("aaaa", index.ByPath["Game/A.uasset"]);
        Assert.Equal("bbbb", index.ByPath["Game/A.uexp"]);
        Assert.Equal(0, index.UnfingerprintedCount);
    }

    [Fact]
    public void ByPath_LooksUpCaseInsensitively()
    {
        // The provider is always configured with StringComparer.OrdinalIgnoreCase.
        // IncrementalRunner.ResolvePackagePath's FilesById leg resolves a "packageid:" identity
        // through the provider's own by-id index, which is not guaranteed to hand back a path in
        // the exact same casing this table was built with. A stricter comparer would silently fail
        // that lookup, so the dependency's fingerprint would never be recorded under its identity.
        var index = SourceFingerprintIndex.FromEntries([("Game/A.uasset", "aaaa")]);

        Assert.Equal("aaaa", index.ByPath["GAME/A.UASSET"]);
    }

    [Fact]
    public void FromEntries_CountsEntriesWithNoHash()
    {
        var index = SourceFingerprintIndex.FromEntries(
        [
            ("Game/A.uasset", "aaaa"),
            ("Game/B.uasset", null)
        ]);

        Assert.Single(index.ByPath);
        Assert.Equal(1, index.UnfingerprintedCount);
    }
}
