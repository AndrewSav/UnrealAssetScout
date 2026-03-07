using System;
using System.Diagnostics;

namespace UnrealAssetScout.Logging;

// Renders a compact progress bar to stderr during export processing, showing progress,
// elapsed time, per-file timing, and live warn/error counts from LogLevelCounterSink.
// Created in ExportProcessor.ProcessFiles when compact mode is active, and driven by
// SetCurrentFile/RecordCompletedFile/Render/Complete calls from the per-file processing loop.
internal sealed class CompactProgress(int totalWorkItems, LogLevelCounterSink counterSink)
{
    private const int MinimumRenderWidth = 20;
    private const string Ellipsis = "...";
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private string _currentFile = string.Empty;
    private long _lastRenderMillis = -1;
    private bool _hasDrawn;
    private TimeSpan _lastCompletedFileDuration;
    private TimeSpan _maxCompletedFileDuration;
    private bool _hasCompletedFileTiming;

    public void Render(int processedWorkItems)
    {
        var elapsedMillis = _stopwatch.ElapsedMilliseconds;
        if (processedWorkItems < totalWorkItems && _lastRenderMillis >= 0 && elapsedMillis - _lastRenderMillis < 120)
            return;

        _lastRenderMillis = elapsedMillis;
        WriteLine(processedWorkItems);
    }

    public void SetCurrentFile(string path, int processedWorkItems)
    {
        _currentFile = path;
        _lastRenderMillis = _stopwatch.ElapsedMilliseconds;
        WriteLine(processedWorkItems);
    }

    public void Complete(int processedWorkItems)
    {
        WriteLine(processedWorkItems);
        if (_hasDrawn)
            Console.Error.WriteLine();
        Console.Error.WriteLine();
    }

    public void RecordCompletedFile(TimeSpan duration)
    {
        _lastCompletedFileDuration = duration;
        if (!_hasCompletedFileTiming || duration > _maxCompletedFileDuration)
            _maxCompletedFileDuration = duration;
        _hasCompletedFileTiming = true;
    }

    private void WriteLine(int processedWorkItems)
    {
        var clampedProcessed = Math.Clamp(processedWorkItems, 0, totalWorkItems);
        var completedRatio = totalWorkItems == 0 ? 1.0 : (double) clampedProcessed / totalWorkItems;
        var remainingPercent = 100.0 * (1.0 - completedRatio);
        var warningCount = counterSink.WarningCount;
        var errorCount = counterSink.ErrorCount;
        var previousFileText = _hasCompletedFileTiming ? Utils.Formatting.FormatMilliseconds(_lastCompletedFileDuration.TotalMilliseconds) : "-";
        var maxFileText = _hasCompletedFileTiming ? Utils.Formatting.FormatMilliseconds(_maxCompletedFileDuration.TotalMilliseconds) : "-";

        const int barWidth = 20;
        var filled = Math.Clamp((int) Math.Round(completedRatio * barWidth, MidpointRounding.AwayFromZero), 0, barWidth);
        var bar = new string('#', filled) + new string('-', barWidth - filled);
        var summary = $"[{bar}] {remainingPercent,6:0.0}% remaining | elapsed {Utils.Formatting.FormatElapsed(_stopwatch.Elapsed)} | prev {previousFileText} max {maxFileText} | warn {warningCount} err {errorCount} | {clampedProcessed}/{totalWorkItems}";
        var fileLine = "file: " + (_currentFile.Length == 0 ? "-" : _currentFile);

        if (!Console.IsErrorRedirected)
        {
            var renderWidth = GetInteractiveWidth();
            summary = FitToWidth(summary, renderWidth);
            fileLine = FitToWidth(fileLine, renderWidth);

            if (_hasDrawn)
                Console.Error.Write("\u001b[1A");

            Console.Error.Write("\r\u001b[2K");
            Console.Error.Write(summary);
            Console.Error.WriteLine();
            Console.Error.Write("\r\u001b[2K");
            Console.Error.Write(fileLine);
            _hasDrawn = true;
            return;
        }

        Console.Error.Write($"\r{summary} | {fileLine}");
        _hasDrawn = true;
    }

    private static int GetInteractiveWidth()
    {
        try
        {
            return Math.Max(MinimumRenderWidth, Console.BufferWidth - 1);
        }
        catch
        {
            return 120;
        }
    }

    private static string FitToWidth(string value, int width)
    {
        if (width <= 0 || value.Length <= width)
            return value;

        if (width <= Ellipsis.Length)
            return value[..width];

        return Ellipsis + value[^Math.Max(1, width - Ellipsis.Length)..];
    }

}
