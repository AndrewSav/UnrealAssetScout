using System.Collections.Generic;
using System.IO;
using System.Linq;
using CUE4Parse.FileProvider;
using UnrealAssetScout.Config;
using UnrealAssetScout.Logging;
using UnrealAssetScout.Package;

namespace UnrealAssetScout.List;

// Main listing engine for runs where no export mode is specified.
// Called from Program.Main after the provider is mounted to enumerate matching files, emit plain
// list output, folder-tree output, or CSV export-composition rows depending on the selected
// list format, while optionally annotating plain list output with usmap hints.
internal static class ListProcessor
{
    internal static void ListFiles(
        DefaultFileProvider provider,
        Options options,
        TextWriter? outputFileWriter = null,
        IReadOnlySet<string>? typeFilteredPaths = null)
    {
        AppLog.Information("Found {Count} files", provider.Files.Count);
        var matchingPaths = provider.Files.Keys
            .Where(path =>
                (options.Filter is null || options.Filter.IsMatch(path)) &&
                (typeFilteredPaths is null || typeFilteredPaths.Contains(path)))
            .ToArray();

        if (options.ListFormat == ListOutputFormat.Tree)
        {
            foreach (var line in FolderTreeRenderer.RenderFolders(matchingPaths))
                WriteOutputLine(line, outputFileWriter);
            return;
        }

        var usmapRequiredCount = 0;
        var matchingFileCount = matchingPaths.Length;
        var typesFormatEnabled = options.ListFormat == ListOutputFormat.Types;
        var markUsmapEnabled = options.MarkUsmap && !typesFormatEnabled;

        if (typesFormatEnabled)
            WriteOutputLine("Path,Type,Count", outputFileWriter);

        var listedFileIndex = 0;
        foreach (var (path, file) in provider.Files)
        {
            if ((options.Filter is not null && !options.Filter.IsMatch(path)) ||
                (typeFilteredPaths is not null && !typeFilteredPaths.Contains(path)))
                continue;

            listedFileIndex++;
            var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
            string prefix = "";

            if (ext is "uasset" or "umap")
            {
                if (typesFormatEnabled)
                {
                    var packageContext = PackageLoadSupport.PreparePackageForExport(provider, file, markUsmap: false);
                    using var packageFileLogContext =
                        RuntimeLogging.PushFileProgressContext(listedFileIndex, matchingFileCount, options.LogCounter);
                    foreach (var row in ListExportSummaryFormatter.FormatPackageExports(path, packageContext))
                        WriteOutputLine(row, outputFileWriter);
                    continue;
                }

                if (markUsmapEnabled)
                {
                    var requirement = PackageLoadSupport.NeedsUsmap(provider, file);
                    prefix = PackageLoadSupport.GetUsmapPrefix(true, requirement);
                    if (requirement == UsmapRequirement.RequiresUsmap)
                        usmapRequiredCount++;
                }
            }
            else
            {
                if (markUsmapEnabled)
                    prefix = "[ ] ";

                if (typesFormatEnabled)
                {
                    using var nonPackageFileLogContext =
                        RuntimeLogging.PushFileProgressContext(listedFileIndex, matchingFileCount, options.LogCounter);
                    foreach (var row in ListExportSummaryFormatter.FormatNoExports(path))
                        WriteOutputLine(row, outputFileWriter);
                    continue;
                }
            }

            var listedPath = prefix + path;
            using var fileLogContext =
                RuntimeLogging.PushFileProgressContext(listedFileIndex, matchingFileCount, options.LogCounter);
            WriteOutputLine(listedPath, outputFileWriter);
        }

        if (markUsmapEnabled)
            AppLog.Information("Files requiring usmap: {Count}", usmapRequiredCount);
    }

    internal static void WriteOutputLine(string line, TextWriter? outputFileWriter)
    {
        RuntimeLogging.LogPlainOutputLine(line);
        outputFileWriter?.WriteLine(line);
    }
}
