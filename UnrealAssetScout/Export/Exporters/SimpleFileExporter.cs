using System;
using System.IO;
using CUE4Parse.UE4.AssetRegistry;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.BinaryConfig;
using CUE4Parse.UE4.CriWare.Readers;
using CUE4Parse.UE4.FMod;
using CUE4Parse.UE4.Localization;
using CUE4Parse.UE4.Shaders;
using CUE4Parse.UE4.Wwise;

namespace UnrealAssetScout.Export.Exporters;

// Exports non-package files for simple mode, including known typed formats and raw-file fallback.
// Called by ExportProcessor.ExportSimpleAsset so disk-write logic lives with other exporters while
// ExportProcessor keeps ownership of runtime logging and mode-stat accounting.
internal static class SimpleFileExporter
{
    internal static SimpleFileExportResult Export(ExportItemInfo item, string outputDir)
    {
        var (specializedResult, statKey) = TryExportKnownFile(item, outputDir);
        if (specializedResult.Succeeded)
            return new SimpleFileExportResult(specializedResult, ExportAttemptResult.NotHandled(), statKey);

        var rawFallbackResult = ExportRaw(item, outputDir);
        return new SimpleFileExportResult(specializedResult, rawFallbackResult, statKey);
    }

    private static (ExportAttemptResult Result, string StatKey) TryExportKnownFile(ExportItemInfo item, string outputDir)
    {
        var path = item.Path;
        var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();

        try
        {
            return ext switch
            {
                "locres" => (SimpleExportSupport.TryExportJson<FTextLocalizationResource>(item, outputDir), ".locres"),
                "locmeta" => (SimpleExportSupport.TryExportJson<FTextLocalizationMetaDataResource>(item, outputDir), ".locmeta"),
                _ when path.EndsWith("AssetRegistry.bin", StringComparison.OrdinalIgnoreCase) =>
                    (SimpleExportSupport.TryExportJson<FAssetRegistryState>(item, outputDir), "AssetRegistry.bin"),
                "bin" when path.Contains("GlobalShaderCache", StringComparison.OrdinalIgnoreCase) =>
                    (SimpleExportSupport.TryExportJson<FGlobalShaderCache>(item, outputDir), "GlobalShaderCache.bin"),
                "ini" when path.Contains("BinaryConfig", StringComparison.OrdinalIgnoreCase) =>
                    (SimpleExportSupport.TryExportJson<FConfigCacheIni>(item, outputDir), "BinaryConfig.ini"),
                "ushaderbytecode" or "ushadercode" => (SimpleExportSupport.TryExportJson<FShaderCodeArchive>(item, outputDir), "." + ext),
                "upipelinecache" => (SimpleExportSupport.TryExportJson<FPipelineCacheFile>(item, outputDir), ".upipelinecache"),
                "stinfo" => (SimpleExportSupport.TryExportJson<FShaderTypeHashes>(item, outputDir), ".stinfo"),
                "udic" => (OodleDictionaryExporter.TryExport(item, outputDir), ".udic"),
                "bank" => (AudioBankExporter.TryExport<FModProvider>(item, outputDir), ".bank"),
                "bnk" or "pck" => (AudioBankExporter.TryExport<WwiseProvider>(item, outputDir), "." + ext),
                "awb" => (CriWareExporter.TryExport<AwbReader>(item, outputDir), ".awb"),
                "acb" => (CriWareExporter.TryExport<AcbReader>(item, outputDir), ".acb"),
                _ => (ExportAttemptResult.NotHandled(), string.Empty)
            };
        }
        catch (Exception e)
        {
            return (ExportAttemptResult.Failure(path, e.Message), string.Empty);
        }
    }

    internal static ExportAttemptResult ExportRaw(ExportItemInfo item, string outputDir)
    {
        var path = item.Path;

        try
        {
            if (!item.Provider.TrySaveAsset(item.File, out var data))
                return ExportAttemptResult.Failure(path, "could not save raw file");

            var outPath = Path.Combine(outputDir, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            File.WriteAllBytes(outPath, data);
            return ExportAttemptResult.Success(path, outPath);
        }
        catch (Exception e)
        {
            return ExportAttemptResult.Failure(path, e.Message);
        }
    }
}
