using CUE4Parse.UE4.Assets.Exports;
using UnrealAssetScout.Export.Exporters;
using UnrealAssetScout.Package;
using UnrealAssetScout.Statistics;

namespace UnrealAssetScout.Export.Processors;

// Processes package exports for verse mode.
// Created by ExportProcessor.ProcessFiles for ExportMode.Verse, then passed to
// ExportProcessor.ProcessPackageMode to export verse assets from the loaded package.
internal sealed class VersePackageProcessor(string outputDir, bool verbose, ModeStatsAccumulator modeStats)
    : PackageModeProcessorBase(outputDir, verbose, modeStats)
{
    protected override ExportAttemptResult TryExport(UObject export, PackageExportContext packageContext) =>
        VerseExporter.TryExport(export, packageContext, OutputDir);

    protected override string NoExportsReason => "no verse exports";
}
