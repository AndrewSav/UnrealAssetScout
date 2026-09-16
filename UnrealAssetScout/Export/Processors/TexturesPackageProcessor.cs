using System.Collections.Generic;
using System.Linq;
using CUE4Parse.GameTypes.KRD.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Texture;
using UnrealAssetScout.Export.Exporters;
using UnrealAssetScout.Package;
using UnrealAssetScout.Statistics;

namespace UnrealAssetScout.Export.Processors;

// Processes package exports for textures mode.
// Created by ExportProcessor.ProcessFiles for ExportMode.Textures, then passed to
// ExportProcessor.ProcessPackageMode to export textures and SVG assets from each loaded package,
// and to decide whether a package's outputs need a directory of their own.
internal sealed class TexturesPackageProcessor(string outputDir, bool verbose, ModeStatsAccumulator modeStats)
    : PackageModeProcessorBase(outputDir, verbose, modeStats)
{
    // Counts only what this mode writes, because a map package carries thousands of exports it
    // never turns into an image, and counting those would nest every single-texture package.
    internal static bool WritesMoreThanOneFile(IReadOnlyList<UObject> exports) =>
        exports.Count(export => export is UTexture or USvgAsset) > 1;

    protected override bool ShouldNestUnderPackage(IReadOnlyList<UObject> exports) =>
        WritesMoreThanOneFile(exports);

    protected override ExportAttemptResult TryExport(UObject export, PackageExportContext packageContext, bool nestUnderPackage) =>
        export switch
        {
            UTexture texture => TextureExporter.TryExport(texture, packageContext, OutputDir, nestUnderPackage),
            USvgAsset svgAsset => SvgExporter.TryExport(svgAsset, packageContext, OutputDir, nestUnderPackage),
            _ => ExportAttemptResult.NotHandled()
        };

    protected override string NoExportsReason => "no texture exports";
}
