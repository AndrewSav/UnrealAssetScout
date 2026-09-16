using System;
using System.Linq;
using CUE4Parse_Conversion;
using CUE4Parse_Conversion.Options;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Actor;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using UnrealAssetScout.Package;

namespace UnrealAssetScout.Export.Exporters;

// Exports supported model- and animation-mode Unreal assets to disk via CUE4Parse_Conversion.
// Called by package-mode processors such as ModelsPackageProcessor and AnimationsPackageProcessor
// when a package export matches one of the supported conversion asset types.
internal static class ConversionExporter
{
    // Deliberately CUE4Parse_Conversion's own defaults rather than a pinned set, so exports
    // follow the formats it considers current.
    private static readonly ExportOptions ConversionOptions = new();

    internal static ExportAttemptResult TryExportModel(UObject export, PackageExportContext packageContext, string outputDir)
    {
        if (export is not (UMaterialInterface or USkeletalMesh or USkeleton or UStaticMesh or ALandscapeProxy))
            return ExportAttemptResult.NotHandled();

        return TryExport(export, packageContext, outputDir);
    }

    internal static ExportAttemptResult TryExportAnimation(UObject export, PackageExportContext packageContext, string outputDir)
    {
        if (export is not (UAnimSequence or UAnimMontage or UAnimComposite))
            return ExportAttemptResult.NotHandled();

        return TryExport(export, packageContext, outputDir);
    }

    private static ExportAttemptResult TryExport(UObject export, PackageExportContext packageContext, string outputDir)
    {
        var logPath = $"{packageContext.Path}/{export.Name}";

        try
        {
            var session = new ExportSession();
            session.Add(export);
            var results = session.RunAsync(outputDir, ConversionOptions).GetAwaiter().GetResult();

            var failed = results.FirstOrDefault(result => !result.Success);
            if (failed is not null)
                return ExportAttemptResult.Failure(logPath, failed.Error?.Message ?? "conversion failed");

            // The origin is the file itself, not the export that pulled it in. One run writes the
            // asset plus every texture it references, and a texture shared by many assets is written
            // once per asset; ExportResult reports files per queued object, not per file, so the
            // path is the only per-file identity available. CUE4Parse names each file after the
            // asset it represents, at that asset's own package path, so equal paths are equal data.
            var exportedArtifacts = results
                .SelectMany(result => result.DiskFilePaths ?? [])
                .Select(diskFilePath => new ExportedArtifact(
                    logPath, diskFilePath, new ArtifactOrigin(diskFilePath, null)))
                .ToArray();

            return ExportAttemptResult.Success(exportedArtifacts);
        }
        catch (Exception e)
        {
            return ExportAttemptResult.Failure(logPath, e.Message);
        }
    }
}
