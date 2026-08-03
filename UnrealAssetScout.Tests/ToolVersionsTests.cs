using System.Reflection;
using UnrealAssetScout.Incremental;

namespace UnrealAssetScout.Tests;

public sealed class ToolVersionsTests
{
    [Fact]
    public void Current_ReturnsNonEmptyVersionsForBothComponents()
    {
        var pair = ToolVersions.Current;

        Assert.False(string.IsNullOrWhiteSpace(pair.Uas));
        Assert.False(string.IsNullOrWhiteSpace(pair.Cue4Parse));
    }

    [Fact]
    public void Current_IncludesGitShaWhenStamped()
    {
        var pair = ToolVersions.Current;

        // The build stamps "<version>+<sha>". In a git checkout both halves are present.
        // "unknown" is the documented fallback when git is unavailable, and is still non-empty.
        Assert.Contains("+", pair.Uas);
        Assert.Contains("+", pair.Cue4Parse);
    }

    [Fact]
    public void Current_IsStableAcrossCalls()
    {
        Assert.Equal(ToolVersions.Current, ToolVersions.Current);
    }

    [Fact]
    public void Current_ReflectsTheStampedAssemblyMetadata()
    {
        var assembly = typeof(ToolVersions).Assembly;
        var metadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().ToList();

        var uasGitSha = metadata.FirstOrDefault(attribute => attribute.Key == "UasGitSha")?.Value;
        var cue4ParseGitSha = metadata.FirstOrDefault(attribute => attribute.Key == "Cue4ParseGitSha")?.Value;

        // The MSBuild target stamps both keys onto this assembly. An absent attribute means the
        // target did not run at all, which is a stronger failure than a fallback value.
        Assert.False(string.IsNullOrEmpty(uasGitSha));
        Assert.False(string.IsNullOrEmpty(cue4ParseGitSha));

        var pair = ToolVersions.Current;

        // Tie Current back to the metadata read independently above, so a broken key lookup in
        // ToolVersions.GetMetadata shows up here even though it would still satisfy the simpler
        // non-empty and contains-"+" assertions above.
        Assert.EndsWith($"+{uasGitSha}", pair.Uas);
        Assert.EndsWith($"+{cue4ParseGitSha}", pair.Cue4Parse);
    }
}
