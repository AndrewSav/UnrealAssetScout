using System;
using System.IO;
using Serilog;
using Serilog.Context;
using Serilog.Sinks.SystemConsole.Themes;

namespace UnrealAssetScout.Logging;

// Static class that owns logger configuration for both UnrealAssetScout and dependency logging.
// Called from Program.Main to set up the UnrealAssetScout bootstrap logger, prepare the log file,
// and configure the runtime logger split so UnrealAssetScout output can stay visible while
// dependency logs on the global Serilog logger remain suppressible.
internal static class RuntimeLogging
{
    private const string PlainOutputProperty = "PlainOutput";
    private const string FileProgressProperty = "FileProgressPrefix";
    private const string ExternalProperty = "External";

    internal static void ConfigureBootstrapLogger()
    {
        CloseAndFlush();
        var bootstrapLogger = new LoggerConfiguration()
            .WriteTo.Console(theme: AnsiConsoleTheme.Literate)
            .CreateLogger();
        AppLog.SetLogger(bootstrapLogger);
        Log.Logger = CreateSilentLogger();
    }

    internal static LogLevelCounterSink? ReConfigureLogger(
        bool compactProgressEnabled,
        bool fileLoggingEnabled,
        string logFilePath,
        bool logLibrariesEnabled)
    {
        CloseAndFlush();
        var loggerConfig = new LoggerConfiguration()
            .Enrich.FromLogContext();
        LogLevelCounterSink? counterSink = null;

        if (compactProgressEnabled)
        {
            counterSink = new LogLevelCounterSink();
            loggerConfig.WriteTo.Sink(counterSink);
        }
        else
        {
            loggerConfig.WriteTo.Logger(lc =>
            {
                lc.Filter.ByExcluding(e =>
                    e.Properties.ContainsKey(PlainOutputProperty) ||
                    e.Properties.ContainsKey(ExternalProperty));
                lc.WriteTo.Console(theme: AnsiConsoleTheme.Literate);
            });
            loggerConfig.WriteTo.Logger(lc =>
            {
                lc.Filter.ByIncludingOnly(e => e.Properties.ContainsKey(ExternalProperty));
                lc.WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [External] {Message:lj}{NewLine}{Exception}",
                    theme: ConsoleTheme.None);
            });
            loggerConfig.WriteTo.Logger(lc =>
            {
                lc.Filter.ByIncludingOnly(e => e.Properties.ContainsKey(PlainOutputProperty));
                lc.WriteTo.Console(outputTemplate: "{Message:lj}{NewLine}", theme: ConsoleTheme.None);
            });
        }

        if (fileLoggingEnabled)
            loggerConfig.WriteTo.File(
                new PlainAwareFormatter(PlainOutputProperty, FileProgressProperty, ExternalProperty),
                logFilePath);

        var rootLogger = loggerConfig.CreateLogger();
        AppLog.SetLogger(rootLogger);
        Log.Logger = logLibrariesEnabled
            ? rootLogger.ForContext(ExternalProperty, true)
            : CreateSilentLogger();
        return counterSink;
    }

    internal static void LogPlainOutputLine(string line) =>
        AppLog.ForContext(PlainOutputProperty, true).Information("{Line}", line);

    internal static IDisposable PushFileProgressContext(int current, int total, bool enabled)
        => enabled
            ? LogContext.PushProperty(
                FileProgressProperty,
                $"[{current.ToString().PadLeft(total.ToString().Length)}/{total}] ")
            : NoopDisposable.Instance;

    internal static void PrepareLogFile(string logFilePath, bool logAppend)
    {
        var logDirectory = Path.GetDirectoryName(logFilePath);
        if (!string.IsNullOrWhiteSpace(logDirectory))
            Directory.CreateDirectory(logDirectory);

        if (logAppend || !File.Exists(logFilePath))
            return;

        File.Delete(logFilePath);
    }

    internal static void CloseAndFlush()
    {
        var appLogger = AppLog.Logger;
        var dependencyLogger = Log.Logger;

        AppLog.SetLogger(CreateSilentLogger());
        Log.Logger = CreateSilentLogger();

        if (ReferenceEquals(appLogger, dependencyLogger))
        {
            DisposeLogger(appLogger);
            return;
        }

        DisposeLogger(appLogger);
        DisposeLogger(dependencyLogger);
    }

    // A disposable placeholder used when file-progress logging context is disabled.
    // Returned by RuntimeLogging.PushFileProgressContext so callers can always dispose the result
    // without branching on whether a Serilog context property was actually pushed.
    private sealed class NoopDisposable : IDisposable
    {
        internal static readonly NoopDisposable Instance = new();

        public void Dispose()
        {
        }
    }

    private static ILogger CreateSilentLogger() =>
        new LoggerConfiguration().CreateLogger();

    private static void DisposeLogger(ILogger logger)
    {
        if (logger is IDisposable disposableLogger)
            disposableLogger.Dispose();
    }
}
