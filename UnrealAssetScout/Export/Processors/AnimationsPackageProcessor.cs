using CUE4Parse.UE4.Assets.Exports;
using UnrealAssetScout.Export.Exporters;
using UnrealAssetScout.Package;
using UnrealAssetScout.Statistics;

namespace UnrealAssetScout.Export.Processors;

// Processes package exports for animations mode.
// Created by ExportProcessor.ProcessFiles for ExportMode.Animations, then passed to
// ExportProcessor.ProcessPackageMode to export supported animation assets from each loaded package.
internal sealed class AnimationsPackageProcessor(string outputDir, bool verbose, ModeStatsAccumulator modeStats)
    : PackageModeProcessorBase(outputDir, verbose, modeStats)
{
    protected override ExportAttemptResult TryExport(UObject export, PackageExportContext packageContext) =>
        ConversionExporter.TryExportAnimation(export, packageContext, OutputDir);

    protected override string NoExportsReason => "no animation exports";
}
