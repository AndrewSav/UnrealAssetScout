using System;
using System.Collections.Generic;
using CUE4Parse.UE4.Assets.Exports;

namespace UnrealAssetScout.Export.Exporters;

// Writes a json-mode package's exports to one JSON file, streamed by ExportPathUtils.WriteJson.
// Called by JsonPackageProcessor for every package its skip list lets through.
internal static class PackageJsonExporter
{
    internal static ExportAttemptResult TryExport(string path, string outputDir, IReadOnlyCollection<UObject> exports)
    {
        var outPath = ExportPathUtils.ToOutputPath(outputDir, path, ".json");
        try
        {
            ExportPathUtils.WriteJson(outPath, exports);
            return ExportAttemptResult.Success(path, outPath);
        }
        catch (Exception e)
        {
            return ExportAttemptResult.Failure(path, e.Message);
        }
    }
}
