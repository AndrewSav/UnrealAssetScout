using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CUE4Parse.FileProvider.Vfs;
using UnrealAssetScout.Export.Exporters;
using UnrealAssetScout.Export.Processors;
using UnrealAssetScout.Incremental;
using UnrealAssetScout.Logging;
using UnrealAssetScout.Package;
using UnrealAssetScout.Statistics;

namespace UnrealAssetScout.Export;

// Main export engine. Called from Program.Main for export runs. Iterates over all files in the
// CUE4Parse provider, dispatches to mode-specific export logic, and drives the
// CompactProgress display when active. When an incremental work list, constituent lookup and
// SourceRecorder are supplied, restricts iteration to the work list and opens and closes a source
// record around each processed file so the recorder can be handed to ManifestBuilder afterward.
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
        IReadOnlyCollection<string> jsonSkipTypeNames,
        IReadOnlySet<string>? incrementalWorkList = null,
        SourceRecorder? recorder = null,
        Func<string, IReadOnlyList<string>?>? constituentsOf = null)
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
                var matchesWorkList = incrementalWorkList is null || incrementalWorkList.Contains(path);
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
                if (matchesRegexFilter && matchesTypeFilter && matchesWorkList && processingDecision.ShouldProcess)
                    totalWorkItems++;

                return new
                {
                    File = file,
                    Path = path,
                    Extension = ext,
                    MatchesRegexFilter = matchesRegexFilter,
                    MatchesTypeFilter = matchesTypeFilter,
                    MatchesWorkList = matchesWorkList,
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

            if (!entry.MatchesRegexFilter || !entry.MatchesTypeFilter || !entry.MatchesWorkList || !shouldProcess)
            {
                if (verbose)
                {
                    var skipReason = !entry.MatchesRegexFilter
                        ? "filter mismatch"
                        : !entry.MatchesTypeFilter
                            ? "type expression mismatch"
                            : !entry.MatchesWorkList
                                ? "unchanged (incremental)"
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
                    runStatsAccumulator.RecordRequirement(ProcessPackageMode(
                        item, markUsmap,
                        new JsonPackageProcessor(outputDir, verbose, jsonSkipTypeNames),
                        recorder, constituentsOf?.Invoke(path)));
                    break;

                case ExportMode.Textures:
                    runStatsAccumulator.ModeStats.SetSummaryLabel("Texture export(s)");
                    runStatsAccumulator.RecordRequirement(ProcessPackageMode(
                        item, markUsmap,
                        new TexturesPackageProcessor(outputDir, verbose, runStatsAccumulator.ModeStats),
                        recorder, constituentsOf?.Invoke(path)));
                    break;

                case ExportMode.Models:
                    runStatsAccumulator.ModeStats.SetSummaryLabel("Model export(s)");
                    runStatsAccumulator.RecordRequirement(ProcessPackageMode(
                        item, markUsmap,
                        new ModelsPackageProcessor(outputDir, verbose, runStatsAccumulator.ModeStats),
                        recorder, constituentsOf?.Invoke(path)));
                    break;

                case ExportMode.Animations:
                    runStatsAccumulator.ModeStats.SetSummaryLabel("Animation export(s)");
                    runStatsAccumulator.RecordRequirement(ProcessPackageMode(
                        item, markUsmap,
                        new AnimationsPackageProcessor(outputDir, verbose, runStatsAccumulator.ModeStats),
                        recorder, constituentsOf?.Invoke(path)));
                    break;

                case ExportMode.Audio:
                    runStatsAccumulator.ModeStats.SetSummaryLabel("Audio export(s)");
                    runStatsAccumulator.RecordRequirement(ProcessPackageMode(
                        item, markUsmap,
                        new AudioPackageProcessor(item, outputDir, verbose, runStatsAccumulator.ModeStats, recorder),
                        recorder, constituentsOf?.Invoke(path)));
                    break;

                case ExportMode.Verse:
                    runStatsAccumulator.ModeStats.SetSummaryLabel("Verse export(s)");
                    runStatsAccumulator.RecordRequirement(ProcessPackageMode(
                        item, markUsmap,
                        new VersePackageProcessor(outputDir, verbose, runStatsAccumulator.ModeStats),
                        recorder, constituentsOf?.Invoke(path)));
                    break;

                case ExportMode.Simple:
                    runStatsAccumulator.ModeStats.SetSummaryLabel("Extractor(s) hits");
                    ExportSimpleAsset(item, outputDir, runStatsAccumulator.ModeStats, recorder);
                    break;

                case ExportMode.Raw:
                    runStatsAccumulator.ModeStats.SetSummaryLabel("Raw file(s) copied");
                    ExportRawAsset(item, outputDir, runStatsAccumulator.ModeStats, recorder);
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

    private static UsmapRequirement ProcessPackageMode(
        ExportItemInfo item,
        bool markUsmap,
        PackageModeProcessorBase processor,
        SourceRecorder? recorder,
        IReadOnlyList<string>? constituents)
    {
        var packageContext = new PackageExportContext(null, item.Path, UsmapRequirement.Unknown, "", PackageLoadResult.Success);
        recorder?.BeginSource(item.Path, constituents ?? [item.Path]);
        var status = SourceStatus.Ok;

        try
        {
            packageContext = PackageLoadSupport.ProcessPackage(item.Provider, item.File, markUsmap, processor.ProcessPackage);
            if (packageContext.LoadResult != PackageLoadResult.Success)
            {
                status = SourceStatus.Failed;
                AppLog.Warning("[FAILED]   {Prefix}{Path}: could not load package", packageContext.Prefix, packageContext.Path);
            }
            else if (packageContext.Package is { } package)
            {
                recorder?.ObservePackage(package, item.Provider);
                status = ResolveStatus(processor);
            }
        }
        catch (Exception e)
        {
            status = SourceStatus.Failed;
            AppLog.Warning("[FAILED]   {Path}: {Message}", item.Path, e.Message);
        }

        recorder?.AddArtifacts(processor.RecordedArtifacts);
        recorder?.EndSource(status);
        return packageContext.Requirement;
    }

    // Skip-list is checked first: a skipped package never attempts an export, so it never sets
    // HasFailure. Any failure elsewhere marks the whole source Failed even when other exports from
    // the same package succeeded; SourceStatus has no partial state.
    internal static string ResolveStatus(PackageModeProcessorBase processor) => processor switch
    {
        JsonPackageProcessor { SkippedBySkipList: true } => SourceStatus.SkippedBySkipList,
        { HasFailure: true } => SourceStatus.Failed,
        _ => SourceStatus.Ok
    };

    private static void ExportSimpleAsset(ExportItemInfo item, string outputDir, ModeStatsAccumulator modeStats, SourceRecorder? recorder)
    {
        recorder?.BeginSource(item.Path, [item.Path]);
        var exportResult = SimpleFileExporter.Export(item, outputDir, recorder);
        var specializedResult = exportResult.SpecializedResult;
        switch (specializedResult.Status)
        {
            case ExportAttemptStatus.Succeeded:
                foreach (var exportedArtifact in specializedResult.ExportedArtifacts)
                    AppLog.Information("[EXPORTED] {Path} -> {OutPath}", exportedArtifact.LogPath, exportedArtifact.OutputPath);

                modeStats.RecordHit(exportResult.StatKey);
                recorder?.AddArtifacts(specializedResult.ExportedArtifacts);
                recorder?.EndSource(SourceStatus.Ok);
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

                recorder?.AddArtifacts(rawFallbackResult.ExportedArtifacts);
                recorder?.EndSource(SourceStatus.Ok);
                return;

            case ExportAttemptStatus.Failed:
                AppLog.Warning("[FAILED]   {Path}: {Message}", rawFallbackResult.FailurePath, rawFallbackResult.FailureReason);
                recorder?.EndSource(SourceStatus.Failed);
                return;

            case ExportAttemptStatus.NotHandled:
                throw new UnreachableException();

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static void ExportRawAsset(ExportItemInfo item, string outputDir, ModeStatsAccumulator modeStats, SourceRecorder? recorder)
    {
        recorder?.BeginSource(item.Path, [item.Path]);
        var exportResult = SimpleFileExporter.ExportRaw(item, outputDir);
        switch (exportResult.Status)
        {
            case ExportAttemptStatus.Succeeded:
                foreach (var exportedArtifact in exportResult.ExportedArtifacts)
                    AppLog.Information("[EXPORTED] {Path} -> {OutPath}", exportedArtifact.LogPath, exportedArtifact.OutputPath);

                modeStats.RecordHit(Path.GetExtension(item.Path));
                recorder?.AddArtifacts(exportResult.ExportedArtifacts);
                recorder?.EndSource(SourceStatus.Ok);
                return;

            case ExportAttemptStatus.Failed:
                AppLog.Warning("[FAILED]   {Path}: {Message}", exportResult.FailurePath, exportResult.FailureReason);
                recorder?.EndSource(SourceStatus.Failed);
                return;

            case ExportAttemptStatus.NotHandled:
                throw new UnreachableException();

            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}
