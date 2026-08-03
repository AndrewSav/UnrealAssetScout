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
    public void CompactProgress_StillWritesSummaryLinesToTheConsole()
    {
        // The plan and commit summaries describe the run rather than any one file, so they stay
        // visible when compact progress replaces everything else on the console.
        var originalError = Console.Error;
        var captured = new StringWriter();

        try
        {
            Console.SetError(captured);
            RuntimeLogging.ReConfigureLogger(
                compactProgressEnabled: true,
                fileLoggingEnabled: false,
                logFilePath: "unused.log",
                logLibrariesEnabled: false);

            RuntimeLogging.LogSummary("Plan: {Added} to add", 7);
            AppLog.Information("per-file-noise");
            RuntimeLogging.CloseAndFlush();

            var text = captured.ToString();
            Assert.Contains("Plan: 7 to add", text);
            Assert.DoesNotContain("per-file-noise", text);
        }
        finally
        {
            RuntimeLogging.CloseAndFlush();
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void CompactProgress_StillWritesErrorsToTheConsole()
    {
        // Compact mode replaces console output with a progress bar. Without an explicit error
        // sink, a run that stops leaves nothing but a non-zero exit code, and with --no-log there
        // is no log file holding the reason either.
        var originalError = Console.Error;
        var captured = new StringWriter();

        try
        {
            Console.SetError(captured);
            RuntimeLogging.ReConfigureLogger(
                compactProgressEnabled: true,
                fileLoggingEnabled: false,
                logFilePath: "unused.log",
                logLibrariesEnabled: false);

            AppLog.Error("manifest has schema 2, this build expects schema 3");
            AppLog.Information("routine-progress-line");
            RuntimeLogging.CloseAndFlush();

            var text = captured.ToString();
            Assert.Contains("manifest has schema 2, this build expects schema 3", text);
            Assert.DoesNotContain("routine-progress-line", text);
        }
        finally
        {
            RuntimeLogging.CloseAndFlush();
            Console.SetError(originalError);
        }
    }

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
