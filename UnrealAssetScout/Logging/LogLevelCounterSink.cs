using System.Threading;
using Serilog.Core;
using Serilog.Events;

namespace UnrealAssetScout.Logging;

// A Serilog sink that counts warnings and errors as they flow through the logging pipeline.
// Created by RuntimeLogging.ReConfigureLogger when compact progress is enabled, returned to
// Program.Main, and passed to CompactProgress to display live warn/error counts in the progress bar.
internal sealed class LogLevelCounterSink : ILogEventSink
{
    private int _warningCount;
    private int _errorCount;

    public int WarningCount => Volatile.Read(ref _warningCount);
    public int ErrorCount => Volatile.Read(ref _errorCount);

    public void Emit(LogEvent logEvent)
    {
        if (logEvent.Level == LogEventLevel.Warning)
            Interlocked.Increment(ref _warningCount);
        else if (logEvent.Level >= LogEventLevel.Error)
            Interlocked.Increment(ref _errorCount);
    }
}
