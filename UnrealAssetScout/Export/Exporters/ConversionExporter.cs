using System;
using System.IO;
using CUE4Parse_Conversion;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Actor;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using UnrealAssetScout.Package;

namespace UnrealAssetScout.Export.Exporters;

// Exports supported spatial-mode Unreal assets such as meshes, materials, animations, and
// landscapes to disk via CUE4Parse_Conversion.
// Called by ExportProcessor spatial-mode handlers when a package export matches one of the
// supported conversion asset types.
internal static class ConversionExporter
{
    internal static ExportAttemptResult TryExport(UObject export, PackageExportContext packageContext, string outputDir)
    {
        if (export is not (UAnimSequence or UAnimMontage or UAnimComposite or UMaterialInterface or USkeletalMesh or USkeleton or UStaticMesh or ALandscapeProxy))
            return ExportAttemptResult.NotHandled();

        try
        {
            var converterExporter = new Exporter(export, new ExporterOptions());
            if (!converterExporter.TryWriteToDir(new DirectoryInfo(outputDir), out _, out var savedFilePath))
                return ExportAttemptResult.NotHandled();

            return ExportAttemptResult.Success($"{packageContext.Path}/{export.Name}", savedFilePath);
        }
        catch (Exception e)
        {
            return ExportAttemptResult.Failure($"{packageContext.Path}/{export.Name}", e.Message);
        }
    }
}
