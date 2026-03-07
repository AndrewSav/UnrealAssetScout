using System;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Verse;
using UnrealAssetScout.Package;

namespace UnrealAssetScout.Export.Exporters;

// Exports Verse digests as readable `.verse` source files.
// Called by ExportProcessor verse-mode handlers when a package export matches `UVerseDigest`.
internal static class VerseExporter
{
    internal static ExportAttemptResult TryExport(UObject export, PackageExportContext packageContext, string outputDir)
    {
        if (export is not UVerseDigest verseDigest || string.IsNullOrWhiteSpace(verseDigest.ReadableCode))
            return ExportAttemptResult.NotHandled();

        try
        {
            var outPath = ExportPathUtils.ToOutputPath(
                outputDir,
                ExportPathUtils.ComposeRelativeAssetPath(packageContext.Path, export.Name),
                ".verse");
            ExportPathUtils.WriteFile(outPath, verseDigest.ReadableCode);
            return ExportAttemptResult.Success($"{packageContext.Path}/{export.Name}", outPath);
        }
        catch (Exception e)
        {
            return ExportAttemptResult.Failure($"{packageContext.Path}/{export.Name}", e.Message);
        }
    }
}
