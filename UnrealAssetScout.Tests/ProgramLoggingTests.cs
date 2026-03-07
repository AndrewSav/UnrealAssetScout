using UnrealAssetScout.Statistics;
using UnrealAssetScout.Utils;
using Serilog;

namespace UnrealAssetScout.Tests;

[Collection("Logging")]
public class ProgramLoggingTests
{
    [Fact]
    public void WriteCompletionSummary_WritesElapsedAndStats()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var logFilePath = Path.Combine(tempDir, "completion.log");
        var originalError = Console.Error;
        using var errorWriter = new StringWriter();

        try
        {
            Console.SetError(errorWriter);
            RuntimeLogging.ReConfigureLogger(
                compactProgressEnabled: true,
                fileLoggingEnabled: true,
                logFilePath: logFilePath,
                logLibrariesEnabled: false);

            RuntimeReporting.WriteCompletionSummary(
                TimeSpan.FromSeconds(2),
                new RunStats(3, 12.5, 1.25, 20, null, null),
                compactProgressEnabled: true);

            RuntimeLogging.CloseAndFlush();

            var logText = File.ReadAllText(logFilePath);
            Assert.Contains("Elapsed: 00:00:02", logText);
            Assert.Contains("Files processed: 3", logText);
            Assert.Contains("Per-file timing: avg 12.5 ms, stddev 1.25 ms, max 20 ms", logText);

            var errorText = errorWriter.ToString();
            Assert.Contains("Elapsed: 00:00:02", errorText);
            Assert.Contains("Files processed: 3", errorText);
            Assert.Contains("Per-file timing: avg 12.5 ms, stddev 1.25 ms, max 20 ms", errorText);
        }
        finally
        {
            Console.SetError(originalError);
            RuntimeLogging.CloseAndFlush();
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void UnhandledException_UsesSimpleConsoleErrorOutput()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UnrealAssetScout.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var logFilePath = Path.Combine(tempDir, "fatal.log");
        var originalError = Console.Error;
        using var errorWriter = new StringWriter();

        try
        {
            Console.SetError(errorWriter);
            RuntimeLogging.ReConfigureLogger(
                compactProgressEnabled: true,
                fileLoggingEnabled: true,
                logFilePath: logFilePath,
                logLibrariesEnabled: false);

            try
            {
                throw new InvalidOperationException("boom");
            }
            catch (Exception e)
            {
                var exceptionType = e.GetType().FullName ?? e.GetType().Name;
                Console.Error.WriteLine($"Unhandled exception: {exceptionType}: {e.Message}");
            }

            RuntimeLogging.CloseAndFlush();

            var logText = File.ReadAllText(logFilePath);
            Assert.DoesNotContain("Unhandled exception: System.InvalidOperationException: boom", logText);
            Assert.DoesNotContain("Elapsed:", logText);
            Assert.DoesNotContain("Files processed:", logText);
            Assert.DoesNotContain("Per-file timing:", logText);

            var errorText = errorWriter.ToString();
            Assert.Contains("Unhandled exception: System.InvalidOperationException: boom", errorText);
            Assert.DoesNotContain("Elapsed:", errorText);
            Assert.DoesNotContain("Files processed:", errorText);
            Assert.DoesNotContain("Per-file timing:", errorText);
        }
        finally
        {
            Console.SetError(originalError);
            RuntimeLogging.CloseAndFlush();
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
