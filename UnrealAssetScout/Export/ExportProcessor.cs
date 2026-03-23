using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CUE4Parse.FileProvider.Vfs;
using UnrealAssetScout.Export.Exporters;
using UnrealAssetScout.Export.Processors;
using UnrealAssetScout.Logging;
using UnrealAssetScout.Package;
using UnrealAssetScout.Statistics;

namespace UnrealAssetScout.Export;

// Main export engine. Called from Program.Main for export runs. Iterates over all files in the
// CUE4Parse provider, dispatches to mode-specific export logic, and drives the
// CompactProgress display when active.
public static class ExportProcessor
{
    internal static RunStats ProcessFiles(
        AbstractVfsFileProvider provider,
        ExportMode mode,
        string outputDir,
        Regex? filter,
        bool verbose,
        bool markUsmap,
        LogLevelCounterSink? compactCounterSink,
        IReadOnlySet<string>? typeFilteredPaths,
        bool logCounter,
        IReadOnlyCollection<string> jsonSkipTypeNames)
    {
        var mountedPath = provider.MountedVfs.FirstOrDefault()?.Path;
        var gameDirectory = string.IsNullOrWhiteSpace(mountedPath)
            ? string.Empty
            : Path.GetDirectoryName(mountedPath) ?? string.Empty;
        var runStatsAccumulator = new RunStatsAccumulator(markUsmap);
        
        var totalWorkItems = 0;
        var fileDecisions = provider.Files.Values
            .Select(file =>
            {
                var path = file.Path;
                var matchesRegexFilter = filter is null || filter.IsMatch(path);
                var matchesTypeFilter = typeFilteredPaths is null || typeFilteredPaths.Contains(path);
                var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
                (bool ShouldProcess, string SkipReason) processingDecision = mode switch
                {
                    ExportMode.Simple => ext is not ("uasset" or "umap" or "uexp" or "ubulk" or "uptnl") ? (true, "") : (false, "packages are unsupported in simple mode"),
                    ExportMode.Raw => (true, ""),
                    ExportMode.Json => ext is "uasset" or "umap" ? (true, "") : (false, "unsupported extension for json mode"),
                    ExportMode.Textures => ext is "uasset" or "umap" ? (true, "") : (false, "unsupported extension for textures mode"),
                    ExportMode.Models => ext is "uasset" or "umap" ? (true, "") : (false, "unsupported extension for models mode"),
                    ExportMode.Animations => ext is "uasset" or "umap" ? (true, "") : (false, "unsupported extension for animations mode"),
                    ExportMode.Audio => ext is "uasset" or "umap" ? (true, "") : (false, "unsupported extension for audio mode"),
                    ExportMode.Verse => ext == "uasset" ? (true, "") : (false, "unsupported extension for verse mode"),
                    _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported export mode")
                };
                if (matchesRegexFilter && matchesTypeFilter && processingDecision.ShouldProcess)
                    totalWorkItems++;

                return new
                {
                    File = file,
                    Path = path,
                    Extension = ext,
                    MatchesRegexFilter = matchesRegexFilter,
                    MatchesTypeFilter = matchesTypeFilter,
                    ProcessingDecision = processingDecision
                };
            })
            .ToArray();

        var progress = compactCounterSink != null && totalWorkItems > 0
            ? new CompactProgress(totalWorkItems, compactCounterSink)
            : null;
        var processedWorkItems = 0;

        foreach (var entry in fileDecisions)
        {
            var file = entry.File;
            var item = new ExportItemInfo(provider, file, gameDirectory);
            var path = entry.Path;
            var (shouldProcess, decisionSkipReason) = entry.ProcessingDecision;

            if (!entry.MatchesRegexFilter || !entry.MatchesTypeFilter || !shouldProcess)
            {
                if (verbose)
                {
                    var skipReason = !entry.MatchesRegexFilter
                        ? "filter mismatch"
                        : !entry.MatchesTypeFilter
                            ? "type expression mismatch"
                            : decisionSkipReason;
                    AppLog.Information("[SKIPPED]  {Prefix}{Path} ({Reason})", "", path, skipReason);
                }
                continue;
            }

            using var fileLogContext =
                RuntimeLogging.PushFileProgressContext(processedWorkItems + 1, totalWorkItems, logCounter);
            progress?.SetCurrentFile(path, processedWorkItems);
            var fileStopwatch = Stopwatch.StartNew();

            switch (mode)
            {
                case ExportMode.Json:
                    runStatsAccumulator.RecordRequirement(ProcessPackageMode(item, markUsmap, new JsonPackageProcessor(outputDir, verbose, jsonSkipTypeNames)));
                    break;

                case ExportMode.Textures:
                    runStatsAccumulator.ModeStats.SetSummaryLabel("Texture export(s)");
                    runStatsAccumulator.RecordRequirement(ProcessPackageMode(item, markUsmap, new TexturesPackageProcessor(outputDir, verbose, runStatsAccumulator.ModeStats)));
                    break;

                case ExportMode.Models:
                    runStatsAccumulator.ModeStats.SetSummaryLabel("Model export(s)");
                    runStatsAccumulator.RecordRequirement(ProcessPackageMode(item, markUsmap, new ModelsPackageProcessor(outputDir, verbose, runStatsAccumulator.ModeStats)));
                    break;

                case ExportMode.Animations:
                    runStatsAccumulator.ModeStats.SetSummaryLabel("Animation export(s)");
                    runStatsAccumulator.RecordRequirement(ProcessPackageMode(item, markUsmap, new AnimationsPackageProcessor(outputDir, verbose, runStatsAccumulator.ModeStats)));
                    break;

                case ExportMode.Audio:
                    runStatsAccumulator.ModeStats.SetSummaryLabel("Audio export(s)");
                    runStatsAccumulator.RecordRequirement(ProcessPackageMode(item, markUsmap, new AudioPackageProcessor(item, outputDir, verbose, runStatsAccumulator.ModeStats)));
                    break;

                case ExportMode.Verse:
                    runStatsAccumulator.ModeStats.SetSummaryLabel("Verse export(s)");
                    runStatsAccumulator.RecordRequirement(ProcessPackageMode(item, markUsmap, new VersePackageProcessor(outputDir, verbose, runStatsAccumulator.ModeStats)));
                    break;

                case ExportMode.Simple:
                    runStatsAccumulator.ModeStats.SetSummaryLabel("Extractor(s) hits");
                    ExportSimpleAsset(item, outputDir, runStatsAccumulator.ModeStats);
                    break;

                case ExportMode.Raw:
                    runStatsAccumulator.ModeStats.SetSummaryLabel("Raw file(s) copied");
                    ExportRawAsset(item, outputDir, runStatsAccumulator.ModeStats);
                    break;
            }

            fileStopwatch.Stop();
            runStatsAccumulator.AddSample(fileStopwatch.Elapsed.TotalMilliseconds);
            progress?.RecordCompletedFile(fileStopwatch.Elapsed);

            processedWorkItems++;
            progress?.Render(processedWorkItems);
        }

        progress?.Complete(processedWorkItems);
        return runStatsAccumulator.Build();
    }

    private static UsmapRequirement ProcessPackageMode(ExportItemInfo item, bool markUsmap, PackageModeProcessorBase processor)
    {
        var packageContext = new PackageExportContext(null, item.Path, UsmapRequirement.Unknown, "", PackageLoadResult.Success);
        try
        {
            packageContext = PackageLoadSupport.ProcessPackage(item.Provider, item.File, markUsmap, processor.ProcessPackage);
            if (packageContext.LoadResult != PackageLoadResult.Success)
                AppLog.Warning("[FAILED]   {Prefix}{Path}: could not load package", packageContext.Prefix, packageContext.Path);
        }
        catch (Exception e)
        {
            AppLog.Warning("[FAILED]   {Path}: {Message}", item.Path, e.Message);
        }

        return packageContext.Requirement;
    }

    private static void ExportSimpleAsset(ExportItemInfo item, string outputDir, ModeStatsAccumulator modeStats)
    {
        var exportResult = SimpleFileExporter.Export(item, outputDir);
        var specializedResult = exportResult.SpecializedResult;
        switch (specializedResult.Status)
        {
            case ExportAttemptStatus.Succeeded:
                foreach (var exportedArtifact in specializedResult.ExportedArtifacts)
                    AppLog.Information("[EXPORTED] {Path} -> {OutPath}", exportedArtifact.LogPath, exportedArtifact.OutputPath);

                modeStats.RecordHit(exportResult.StatKey);
                return;

            case ExportAttemptStatus.Failed:
                AppLog.Warning("[FAILED]   {Path}: {Message}", specializedResult.FailurePath, specializedResult.FailureReason);
                break;

            case ExportAttemptStatus.NotHandled:
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }

        var rawFallbackResult = exportResult.RawFallbackResult;
        switch (rawFallbackResult.Status)
        {
            case ExportAttemptStatus.Succeeded:
                foreach (var exportedArtifact in rawFallbackResult.ExportedArtifacts)
                    AppLog.Information("[EXPORTED] {Path} -> {OutPath}", exportedArtifact.LogPath, exportedArtifact.OutputPath);
                return;

            case ExportAttemptStatus.Failed:
                AppLog.Warning("[FAILED]   {Path}: {Message}", rawFallbackResult.FailurePath, rawFallbackResult.FailureReason);
                return;

            case ExportAttemptStatus.NotHandled:
                throw new UnreachableException();

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static void ExportRawAsset(ExportItemInfo item, string outputDir, ModeStatsAccumulator modeStats)
    {
        var exportResult = SimpleFileExporter.ExportRaw(item, outputDir);
        switch (exportResult.Status)
        {
            case ExportAttemptStatus.Succeeded:
                foreach (var exportedArtifact in exportResult.ExportedArtifacts)
                    AppLog.Information("[EXPORTED] {Path} -> {OutPath}", exportedArtifact.LogPath, exportedArtifact.OutputPath);

                modeStats.RecordHit(Path.GetExtension(item.Path));
                return;

            case ExportAttemptStatus.Failed:
                AppLog.Warning("[FAILED]   {Path}: {Message}", exportResult.FailurePath, exportResult.FailureReason);
                return;

            case ExportAttemptStatus.NotHandled:
                throw new UnreachableException();

            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}
