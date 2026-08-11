using System.Reflection;
using UnrealAssetScout.Utils;

namespace UnrealAssetScout.Tests;

public sealed class AppVersionTests
{
    [Fact]
    public void BuildFlavor_IsAbsentInAnUnstampedBuild()
    {
        // The test run builds UnrealAssetScout without -p:BuildFlavor, which is the same shape as a
        // developer's local build. Treating that as unpublished is what stops a working copy from
        // replacing itself with a release.
        Assert.Null(AppVersion.BuildFlavor);
        Assert.False(AppVersion.IsPublishedBuild);
    }

    [Fact]
    public void DisplayText_LabelsAnUnstampedBuildAsLocal()
    {
        Assert.Contains("(local build)", AppVersion.DisplayText);
    }

    [Fact]
    public void DisplayText_CarriesTheStampedRevision()
    {
        Assert.Contains(AppVersion.UasGitSha, AppVersion.DisplayText);
    }

    [Fact]
    public void UasVersionText_CarriesBothHalves()
    {
        Assert.Contains("+", AppVersion.UasVersionText);
    }

    [Fact]
    public void UasVersionText_EndsWithTheStampedRevision()
    {
        var metadata = typeof(AppVersion).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToList();

        var uasGitSha = metadata.FirstOrDefault(attribute => attribute.Key == "UasGitSha")?.Value;

        // An absent attribute means the StampGitRevisions target did not run at all, which is a
        // stronger failure than a fallback value.
        Assert.False(string.IsNullOrEmpty(uasGitSha));

        Assert.EndsWith($"+{uasGitSha}", AppVersion.UasVersionText);
    }

    [Fact]
    public void Cue4ParseGitSha_IsABareRevisionWithNoVersion()
    {
        // The manifest records this verbatim. CUE4Parse is pinned by commit, so a version attached
        // here would be noise that changes on its own schedule.
        Assert.DoesNotContain("+", AppVersion.Cue4ParseGitSha);
        Assert.NotEmpty(AppVersion.Cue4ParseGitSha);
    }

    [Fact]
    public void GitShas_MatchTheStampedAssemblyMetadata()
    {
        var metadata = typeof(AppVersion).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToList();

        var uasGitSha = metadata.FirstOrDefault(attribute => attribute.Key == "UasGitSha")?.Value;
        var cue4ParseGitSha = metadata.FirstOrDefault(attribute => attribute.Key == "Cue4ParseGitSha")?.Value;

        Assert.Equal(uasGitSha, AppVersion.UasGitSha);
        Assert.Equal(cue4ParseGitSha, AppVersion.Cue4ParseGitSha);
    }

    [Fact]
    public void VersionText_IsAThreePartVersion()
    {
        // The incremental manifest records this string verbatim and the updater compares it against
        // a three-part release tag, so its shape is a compatibility surface, not a display choice.
        Assert.Equal(3, AppVersion.VersionText.Split('.').Length);
    }

    [Fact]
    public void VersionText_IsTheOnlyRenderingOfCurrent()
    {
        // A four-part Version compares greater than an equal three-part one, so a second rendering
        // reaching a comparison is the bug this single source of truth exists to prevent.
        Assert.Equal(AppVersion.Current.ToString(3), AppVersion.VersionText);
        Assert.StartsWith($"{AppVersion.VersionText}+", AppVersion.DisplayText);
    }
}
