using CUE4Parse.GameTypes.KRD.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Texture;
using UnrealAssetScout.Export.Exporters;
using UnrealAssetScout.Package;
using UnrealAssetScout.Statistics;

namespace UnrealAssetScout.Export.Processors;

// Processes package exports for graphics mode.
// Created by ExportProcessor.ProcessFiles for ExportMode.Graphics, then passed to
// ExportProcessor.ProcessPackageMode to export textures and SVG assets from each loaded package.
internal sealed class GraphicsPackageProcessor(string outputDir, bool verbose, ModeStatsAccumulator modeStats)
    : PackageModeProcessorBase(outputDir, verbose, modeStats)
{
    protected override ExportAttemptResult TryExport(UObject export, PackageExportContext packageContext) =>
        export switch
        {
            UTexture texture => TextureExporter.TryExport(texture, packageContext, OutputDir),
            USvgAsset svgAsset => SvgExporter.TryExport(svgAsset, packageContext, OutputDir),
            _ => ExportAttemptResult.NotHandled()
        };

    protected override string NoExportsReason => "no graphics exports";
}
