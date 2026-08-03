using CUE4Parse.UE4.Assets.Exports;
using UnrealAssetScout.Export.Exporters;
using UnrealAssetScout.Incremental;
using UnrealAssetScout.Package;
using UnrealAssetScout.Statistics;

namespace UnrealAssetScout.Export.Processors;

// Processes package exports for audio mode.
// Created by ExportProcessor.ProcessFiles for ExportMode.Audio with the current ExportItemInfo,
// then passed to ExportProcessor.ProcessPackageMode to export audio assets from the loaded package.
// Forwards its optional SourceRecorder to AudioExporter so Wwise media provenance can be recorded
// alongside the exported artifacts.
internal sealed class AudioPackageProcessor(
    ExportItemInfo item, string outputDir, bool verbose, ModeStatsAccumulator modeStats, SourceRecorder? recorder)
    : PackageModeProcessorBase(outputDir, verbose, modeStats)
{
    protected override ExportAttemptResult TryExport(UObject export, PackageExportContext packageContext) =>
        AudioExporter.TryExport(export, item, packageContext, OutputDir, recorder);

    protected override string NoExportsReason => "no audio exports";
}
