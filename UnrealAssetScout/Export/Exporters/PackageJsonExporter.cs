using System;
using System.Collections.Generic;
using CUE4Parse.UE4.Assets.Exports;
using Newtonsoft.Json;

namespace UnrealAssetScout.Export.Exporters;

// Owns JSON-mode export helpers and package JSON serialization.
// Called by JsonPackageProcessor to decide whether a package should be skipped or written as JSON.
internal static class PackageJsonExporter
{
    internal static ExportAttemptResult TryExport(string path, string outputDir, IReadOnlyCollection<UObject> exports)
    {
        try
        {
            var json = JsonConvert.SerializeObject(exports, Formatting.Indented);
            var outPath = ExportPathUtils.ToOutputPath(outputDir, path, ".json");
            ExportPathUtils.WriteFile(outPath, json);
            return ExportAttemptResult.Success(path, outPath);
        }
        catch (Exception e)
        {
            return ExportAttemptResult.Failure(path, e.Message);
        }
    }
}
