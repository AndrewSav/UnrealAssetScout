using System;
using System.Collections.Generic;
using CUE4Parse.UE4.Readers;
using Newtonsoft.Json;

namespace UnrealAssetScout.Export.Exporters;

// Shared helpers for simple exports that serialize archive content or save extracted files.
// Called by simple exporters such as AudioBankExporter, CriWareExporter, and OodleDictionaryExporter
// to normalize relative paths, write output files, and return structured export results.
internal static class SimpleExportSupport
{
    internal static ExportAttemptResult TryExportJson<T>(ExportItemInfo item, string outputDir)
        where T : class
        => TryExportJson(item, outputDir,
            archive => Activator.CreateInstance(typeof(T), archive)!);

    internal static ExportAttemptResult TryExportJson(ExportItemInfo item, string outputDir, Func<FArchive, object> read)
    {
        var path = item.Path;
        try
        {
            if (!item.File.TryCreateReader(out var archive))
                return ExportAttemptResult.Failure(path, "could not create reader");

            using (archive)
            {
                var payload = read(archive);
                var json = JsonConvert.SerializeObject(payload, Formatting.Indented);
                var outPath = ExportPathUtils.ToOutputPath(outputDir, path, ".json");
                ExportPathUtils.WriteFile(outPath, json);
                return ExportAttemptResult.Success(path, outPath);
            }
        }
        catch (Exception e)
        {
            return ExportAttemptResult.Failure(path, e.Message);
        }
    }

    internal static ExportAttemptResult SaveExtractedFiles(string outputDir, string sourcePath,
        IEnumerable<(string RelativePath, string Extension, byte[] Data)> extracted)
    {
        var exportedArtifacts = new List<ExportedArtifact>();
        foreach (var (relativePath, extension, data) in extracted)
        {
            if (data.Length == 0 || string.IsNullOrWhiteSpace(extension))
                continue;

            var outPath = ExportPathUtils.ToOutputPath(outputDir, relativePath, "." + extension.TrimStart('.').ToLowerInvariant());
            ExportPathUtils.WriteFile(outPath, data);
            exportedArtifacts.Add(new ExportedArtifact(sourcePath, outPath));
        }

        return ExportAttemptResult.Success(exportedArtifacts);
    }

    internal static string NormalizeRelativeDirectory(string path)
    {
        var normalized = path.Replace('\\', '/');
        var lastSlash = normalized.LastIndexOf('/');
        return lastSlash >= 0 ? normalized[..lastSlash] : string.Empty;
    }

    internal static string NormalizeRelativePath(string path) =>
        path.Replace('\\', '/').TrimStart('/');

    internal static string CombineRelativePath(string directory, string name)
    {
        var normalizedName = NormalizeRelativePath(name);
        if (string.IsNullOrEmpty(directory))
            return normalizedName;

        return string.IsNullOrEmpty(normalizedName) ? directory : $"{directory}/{normalizedName}";
    }
}
