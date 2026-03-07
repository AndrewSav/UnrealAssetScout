using CUE4Parse.UE4.Assets.Exports;
using UnrealAssetScout.Export.Exporters;
using UnrealAssetScout.Package;
using UnrealAssetScout.Statistics;

namespace UnrealAssetScout.Export.Processors;

// Processes package exports for spatial mode.
// Created by ExportProcessor.ProcessFiles for ExportMode.Spatial, then passed to
// ExportProcessor.ProcessPackageMode to export supported spatial assets from the loaded package.
internal sealed class ConversionPackageProcessor(string outputDir, bool verbose, ModeStatsAccumulator modeStats)
    : PackageModeProcessorBase(outputDir, verbose, modeStats)
{
    protected override ExportAttemptResult TryExport(UObject export, PackageExportContext packageContext) =>
        ConversionExporter.TryExport(export, packageContext, OutputDir);

    protected override string NoExportsReason => "no spatial exports";
}
