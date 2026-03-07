using System;
using System.Linq;
using CUE4Parse.FileProvider;
using CUE4Parse.FileProvider.Vfs;
using UnrealAssetScout.Logging;
using UnrealAssetScout.Statistics;

namespace UnrealAssetScout.Utils;

// Static helper for end-of-run reporting and AES-related visibility warnings.
// Called from Program.Main after mounting providers and again after listing/export work
// completes, to keep log/console reporting details out of the main entrypoint flow.
internal static class RuntimeReporting
{
    internal static void WriteCompletionSummary(
        TimeSpan elapsed,
        RunStats? runStats,
        bool compactProgressEnabled)
    {
        var elapsedText = Formatting.FormatElapsed(elapsed);
        AppLog.Information("Elapsed: {Elapsed}", elapsedText);
        if (compactProgressEnabled)
            Console.Error.WriteLine($"Elapsed: {elapsedText}");

        if (!runStats.HasValue)
            return;

        var stats = runStats.Value;
        var avgPerFile = Formatting.FormatMilliseconds(stats.AverageMilliseconds);
        var stdDevPerFile = Formatting.FormatMilliseconds(stats.StandardDeviationMilliseconds);
        var maxPerFile = Formatting.FormatMilliseconds(stats.MaximumMilliseconds);

        AppLog.Information("Files processed: {Count}", stats.ProcessedFileCount);
        AppLog.Information("Per-file timing: avg {Average}, stddev {StdDev}, max {Max}", avgPerFile, stdDevPerFile,
            maxPerFile);
        if (stats.UsmapRequiredCount.HasValue)
            AppLog.Information("Files requiring usmap: {Count}", stats.UsmapRequiredCount.Value);
        WriteModeSummary(stats.ModeStats, writeToConsole: false);
        if (!compactProgressEnabled)
            return;

        Console.Error.WriteLine($"Files processed: {stats.ProcessedFileCount}");
        Console.Error.WriteLine($"Per-file timing: avg {avgPerFile}, stddev {stdDevPerFile}, max {maxPerFile}");
        if (stats.UsmapRequiredCount.HasValue)
            Console.Error.WriteLine($"Files requiring usmap: {stats.UsmapRequiredCount.Value}");
        WriteModeSummary(stats.ModeStats, writeToConsole: true);
    }

    internal static void WarnIfAesCouldRevealMore(AbstractFileProvider provider, bool hasExplicitAes)
    {
        if (hasExplicitAes || provider is not IVfsFileProvider vfsProvider)
            return;

        var hiddenByEncryption = vfsProvider.UnloadedVfs
            .Where(v => v.HasDirectoryIndex && (v.IsEncrypted || v.EncryptedFileCount > 0))
            .ToList();

        if (hiddenByEncryption.Count == 0)
            return;

        var hiddenFileEntries = hiddenByEncryption.Sum(v => v.FileCount);
        var allArchives = string.Join(", ",
            hiddenByEncryption.Select(v => v.Name).OrderBy(n => n, StringComparer.Ordinal));

        if (hiddenFileEntries > 0)
        {
            AppLog.Warning(
                "{ArchiveCount} archive(s) with encrypted content are unavailable without an AES key (about {HiddenCount} file entries hidden). Unavailable archive(s): {Archives}",
                hiddenByEncryption.Count, hiddenFileEntries, allArchives);
        }
        else
        {
            AppLog.Warning(
                "{ArchiveCount} archive(s) with encrypted content are unavailable without an AES key. Unavailable archive(s): {Archives}",
                hiddenByEncryption.Count, allArchives);
        }
    }

    private static void WriteModeSummary(ModeStats? modeStats, bool writeToConsole)
    {
        if (!modeStats.HasValue)
            return;

        var stats = modeStats.Value;
        WriteSummaryLine($"{stats.SummaryLabel}: {stats.TotalHitCount}", writeToConsole);
    }

    private static void WriteSummaryLine(string message, bool writeToConsole)
    {
        if (writeToConsole)
            Console.Error.WriteLine(message);
        else
            AppLog.Information("{Message}", message);
    }
}
