using UnrealAssetScout.Config;

namespace UnrealAssetScout.Tests;

public sealed class IncrementalOptionTests
{
    private static Options Parse(params string[] extra)
    {
        string[] baseArgs =
        [
            "export", "json", "--paks", Path.GetTempPath(), "--game", "GAME_UE5_1",
            "--output", Path.GetTempPath()
        ];

        var result = ConfigOptionsSupport.ParseArgsWithExitCode([.. baseArgs, .. extra]);
        Assert.NotNull(result.Options);
        return result.Options;
    }

    [Fact]
    public void Parse_DefaultsAllIncrementalFlagsToFalse()
    {
        var options = Parse();

        Assert.False(options.Rebuild);
        Assert.False(options.DryRun);
        Assert.False(options.AcceptToolVersion);
    }

    [Fact]
    public void Parse_Rebuild_IsRecognised()
    {
        Assert.True(Parse("--rebuild").Rebuild);
    }

    [Fact]
    public void Parse_DryRun_IsRecognised()
    {
        Assert.True(Parse("--dry-run").DryRun);
    }

    [Fact]
    public void Parse_AcceptToolVersion_IsRecognised()
    {
        Assert.True(Parse("--accept-tool-version").AcceptToolVersion);
    }

    [Fact]
    public void Parse_IncrementalFlagsAreAvailableInEveryExportMode()
    {
        foreach (var mode in new[] { "json", "textures", "models", "animations", "audio", "verse", "simple", "raw" })
        {
            var result = ConfigOptionsSupport.ParseArgsWithExitCode(
            [
                "export", mode, "--paks", Path.GetTempPath(), "--game", "GAME_UE5_1",
                "--output", Path.GetTempPath(), "--rebuild", "--dry-run", "--accept-tool-version"
            ]);

            Assert.NotNull(result.Options);
            Assert.True(result.Options.Rebuild, $"--rebuild not accepted in mode {mode}");
        }
    }
}
