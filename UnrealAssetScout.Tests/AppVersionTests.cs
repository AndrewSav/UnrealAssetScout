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
    public void VersionTexts_CarryBothHalvesForBothComponents()
    {
        Assert.Contains("+", AppVersion.UasVersionText);
        Assert.Contains("+", AppVersion.Cue4ParseVersionText);
    }

    [Fact]
    public void VersionTexts_EndWithTheStampedRevisions()
    {
        var metadata = typeof(AppVersion).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToList();

        var uasGitSha = metadata.FirstOrDefault(attribute => attribute.Key == "UasGitSha")?.Value;
        var cue4ParseGitSha = metadata.FirstOrDefault(attribute => attribute.Key == "Cue4ParseGitSha")?.Value;

        // An absent attribute means the StampGitRevisions target did not run at all, which is a
        // stronger failure than a fallback value.
        Assert.False(string.IsNullOrEmpty(uasGitSha));
        Assert.False(string.IsNullOrEmpty(cue4ParseGitSha));

        Assert.EndsWith($"+{uasGitSha}", AppVersion.UasVersionText);
        Assert.EndsWith($"+{cue4ParseGitSha}", AppVersion.Cue4ParseVersionText);
    }

    [Fact]
    public void Cue4ParseVersionText_IsNotEmptyBeforeItsRevisionIsRead()
    {
        // Static initialisers run in declaration order, so a Cue4ParseVersionText declared above
        // Cue4ParseGitSha would silently interpolate an empty revision.
        Assert.DoesNotContain("+$", AppVersion.Cue4ParseVersionText);
        Assert.False(AppVersion.Cue4ParseVersionText.EndsWith('+'));
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
