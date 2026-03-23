using CUE4Parse.UE4.Assets.Exports;
using UnrealAssetScout.Export.Exporters;
using UnrealAssetScout.Package;
using UnrealAssetScout.Statistics;

namespace UnrealAssetScout.Export.Processors;

// Processes package exports for models mode.
// Created by ExportProcessor.ProcessFiles for ExportMode.Models, then passed to
// ExportProcessor.ProcessPackageMode to export meshes, skeletons, materials, and landscapes from
// each loaded package.
internal sealed class ModelsPackageProcessor(string outputDir, bool verbose, ModeStatsAccumulator modeStats)
    : PackageModeProcessorBase(outputDir, verbose, modeStats)
{
    protected override ExportAttemptResult TryExport(UObject export, PackageExportContext packageContext) =>
        ConversionExporter.TryExportModel(export, packageContext, OutputDir);

    protected override string NoExportsReason => "no model exports";
}
