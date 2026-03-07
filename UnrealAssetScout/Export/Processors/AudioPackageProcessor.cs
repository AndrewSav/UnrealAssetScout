using CUE4Parse.UE4.Assets.Exports;
using UnrealAssetScout.Export.Exporters;
using UnrealAssetScout.Package;
using UnrealAssetScout.Statistics;

namespace UnrealAssetScout.Export.Processors;

// Processes package exports for audio mode.
// Created by ExportProcessor.ProcessFiles for ExportMode.Audio with the current ExportItemInfo,
// then passed to ExportProcessor.ProcessPackageMode to export audio assets from the loaded package.
internal sealed class AudioPackageProcessor(ExportItemInfo item, string outputDir, bool verbose, ModeStatsAccumulator modeStats)
    : PackageModeProcessorBase(outputDir, verbose, modeStats)
{
    protected override ExportAttemptResult TryExport(UObject export, PackageExportContext packageContext) =>
        AudioExporter.TryExport(export, item, packageContext, OutputDir);

    protected override string NoExportsReason => "no audio exports";
}
