using UnrealAssetScout.Update;

namespace UnrealAssetScout.Tests;

public sealed class SelfUpdateTests
{
    [Theory]
    [InlineData("framework-dependent", "UnrealAssetScout-v1.2.3.zip")]
    [InlineData("self-contained", "UnrealAssetScout-v1.2.3-self-contained.zip")]
    [InlineData(null, "UnrealAssetScout-v1.2.3.zip")]
    public void AssetNameFor_MatchesWhatTheReleaseWorkflowPublishes(string? buildFlavor, string expected)
    {
        Assert.Equal(expected, SelfUpdate.AssetNameFor(new Version(1, 2, 3), buildFlavor));
    }

    [Fact]
    public void AssetNameFor_DropsTheFourthComponent()
    {
        // Release tags carry three parts, so a four-part assembly version must not leak into the name.
        Assert.Equal("UnrealAssetScout-v1.2.3.zip", SelfUpdate.AssetNameFor(new Version(1, 2, 3, 0), null));
    }

    [Theory]
    [InlineData("v0.3.0", "0.3.0")]
    [InlineData("0.3.0", "0.3.0")]
    [InlineData("V1.20.300", "1.20.300")]
    public void ParseReleaseTag_AcceptsThreePartTags(string tag, string expected)
    {
        Assert.Equal(Version.Parse(expected), SelfUpdate.ParseReleaseTag(tag));
    }

    [Theory]
    [InlineData("latest")]
    [InlineData("v0.3")]
    [InlineData("v0.3.0.1")]
    [InlineData("")]
    [InlineData(null)]
    public void ParseReleaseTag_RejectsAnythingElse(string? tag)
    {
        Assert.Null(SelfUpdate.ParseReleaseTag(tag));
    }

    [Fact]
    public void ParsedTag_ComparesGreaterThanAnOlderRelease()
    {
        // The comparison the updater makes. Both sides are three-part, which is the whole reason
        // AppVersion renders three parts rather than the assembly's four.
        var published = SelfUpdate.ParseReleaseTag("v0.4.0")!;
        Assert.True(published > SelfUpdate.ParseReleaseTag("v0.3.0"));
        Assert.False(published > SelfUpdate.ParseReleaseTag("v0.4.0"));
    }
}
