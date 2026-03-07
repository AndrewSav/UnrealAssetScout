namespace UnrealAssetScout.Tests;

public class LogOptionTests
{
    [Fact]
    public void ParseArgs_WithLogAndLogAppend_SetsLogOptions()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var paksDir = Path.Combine(tempDir, "paks");
        var logPath = Path.Combine(tempDir, "custom.log");

        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(paksDir);

        try
        {
            var options = ConfigOptionsSupport.ParseArgs(
            [
                "list",
                "--paks", paksDir,
                "--game", "GAME_UE5_3",
                "--log", logPath,
                "--log-append"
            ]);

            Assert.NotNull(options);
            Assert.False(options.NoLog);
            Assert.True(options.LogSpecified);
            Assert.True(options.LogAppend);
            Assert.Equal(logPath, options.Log);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseArgs_WithNoLogAndExplicitLogOptions_PreservesAllLogFlags()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        var paksDir = Path.Combine(tempDir, "paks");
        var logPath = Path.Combine(tempDir, "ignored.log");

        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(paksDir);

        try
        {
            var options = ConfigOptionsSupport.ParseArgs(
            [
                "list",
                "--paks", paksDir,
                "--game", "GAME_UE5_3",
                "--no-log",
                "--log", logPath,
                "--log-append"
            ]);

            Assert.NotNull(options);
            Assert.True(options.NoLog);
            Assert.True(options.LogSpecified);
            Assert.True(options.LogAppend);
            Assert.Equal(logPath, options.Log);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

}
