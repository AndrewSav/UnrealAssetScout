using Serilog;

namespace UnrealAssetScout.Tests;

// xUnit collection marker: tests in the "Logging" collection run non-parallel
// because they mutate shared global state (Serilog global logger, console streams,
// and log files), which can otherwise cause flaky cross-test interference.
[CollectionDefinition("Logging", DisableParallelization = true)]
public sealed class LoggingCollectionDefinition;

[Collection("Logging")]
public class RuntimeLoggingTests
{
    [Fact]
    public void PlainOutputLines_AreWrittenWithoutSerilogPrefix()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var logFilePath = Path.Combine(tempDir, "plain-output.log");

        try
        {
            RuntimeLogging.ReConfigureLogger(
                compactProgressEnabled: false,
                fileLoggingEnabled: true,
                logFilePath: logFilePath,
                logLibrariesEnabled: false);

            AppLog.Information("regular-line");
            RuntimeLogging.LogPlainOutputLine("plain-line");
            RuntimeLogging.CloseAndFlush();

            var lines = File.ReadAllLines(logFilePath);
            Assert.Contains(lines, line => line.Contains("regular-line") && line.Contains("[INF]"));
            Assert.Contains("plain-line", lines);
            Assert.DoesNotContain(lines, line => line.Contains("plain-line") && line.Contains("[INF]"));
        }
        finally
        {
            RuntimeLogging.CloseAndFlush();
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void DependencyLogs_AreSuppressedByDefault()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var logFilePath = Path.Combine(tempDir, "dependency-suppressed.log");

        try
        {
            RuntimeLogging.ReConfigureLogger(
                compactProgressEnabled: false,
                fileLoggingEnabled: true,
                logFilePath: logFilePath,
                logLibrariesEnabled: false);

            AppLog.Warning("app-warning");
            Log.Warning("cue4parse-warning");
            RuntimeLogging.CloseAndFlush();

            var logText = File.ReadAllText(logFilePath);
            Assert.Contains("app-warning", logText);
            Assert.DoesNotContain("cue4parse-warning", logText);
        }
        finally
        {
            RuntimeLogging.CloseAndFlush();
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void DependencyLogs_AreWrittenWhenEnabled()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var logFilePath = Path.Combine(tempDir, "dependency-enabled.log");

        try
        {
            RuntimeLogging.ReConfigureLogger(
                compactProgressEnabled: false,
                fileLoggingEnabled: true,
                logFilePath: logFilePath,
                logLibrariesEnabled: true);

            AppLog.Warning("app-warning");
            Log.Warning("cue4parse-warning");
            RuntimeLogging.CloseAndFlush();

            var logText = File.ReadAllText(logFilePath);
            Assert.Contains("app-warning", logText);
            Assert.Contains("[External]", logText);
            Assert.Contains("cue4parse-warning", logText);
        }
        finally
        {
            RuntimeLogging.CloseAndFlush();
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
