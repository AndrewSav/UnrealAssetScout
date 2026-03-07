using UnrealAssetScout.Export;

namespace UnrealAssetScout.Tests;

[Collection("Logging")]
public class SimpleModeOptionTests
{
    [Fact]
    public void ParseArgs_WithSimpleMode_SetsSimpleMode()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var options = ConfigOptionsSupport.ParseArgs(
            [
                "export",
                "simple",
                "--paks", tempDir,
                "--game", "GAME_UE4_27",
                "--output", Path.Combine(tempDir, "out")
            ]);

            Assert.NotNull(options);
            Assert.Equal(ExportMode.Simple, options.Mode);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_WithRawMode_SetsRawMode()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var options = ConfigOptionsSupport.ParseArgs(
            [
                "export",
                "raw",
                "--paks", tempDir,
                "--game", "GAME_UE4_27",
                "--output", Path.Combine(tempDir, "out")
            ]);

            Assert.NotNull(options);
            Assert.Equal(ExportMode.Raw, options.Mode);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
