using System.Collections.Generic;
using System.Linq;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Verse;
using UnrealAssetScout.Export.Exporters;
using UnrealAssetScout.Package;
using UnrealAssetScout.Statistics;

namespace UnrealAssetScout.Export.Processors;

// Processes package exports for verse mode.
// Created by ExportProcessor.ProcessFiles for ExportMode.Verse, then passed to
// ExportProcessor.ProcessPackageMode to export verse assets from the loaded package, and to decide
// whether a package's outputs need a directory of their own.
internal sealed class VersePackageProcessor(string outputDir, bool verbose, ModeStatsAccumulator modeStats)
    : PackageModeProcessorBase(outputDir, verbose, modeStats)
{
    internal static bool WritesMoreThanOneFile(IReadOnlyList<UObject> exports) =>
        exports.Count(export => export is UVerseDigest) > 1;

    protected override bool ShouldNestUnderPackage(IReadOnlyList<UObject> exports) =>
        WritesMoreThanOneFile(exports);

    protected override ExportAttemptResult TryExport(UObject export, PackageExportContext packageContext, bool nestUnderPackage) =>
        VerseExporter.TryExport(export, packageContext, OutputDir, nestUnderPackage);

    protected override string NoExportsReason => "no verse exports";
}
