using System;
using System.Collections.Generic;
using System.Linq;
using CUE4Parse.UE4.Assets.Exports;
using UnrealAssetScout.Export.Exporters;
using UnrealAssetScout.Logging;
using UnrealAssetScout.Package;

namespace UnrealAssetScout.Export.Processors;

// Processes package exports for JSON mode.
// Created by ExportProcessor.ProcessFiles for ExportMode.Json, then passed to
// ExportProcessor.ProcessPackageMode to apply the JSON skip list and write package JSON output.
internal sealed class JsonPackageProcessor(string outputDir, bool verbose, IReadOnlyCollection<string> jsonSkipTypeNames) : PackageModeProcessorBase(outputDir, verbose, null)
{
    private readonly HashSet<string> _jsonSkippedTypeNameSet = new(jsonSkipTypeNames, StringComparer.OrdinalIgnoreCase);

    public override void ProcessPackage(PackageExportContext packageContext)
    {
        var exports = packageContext.Package!.GetExports().ToList();
        if (ShouldSkipJsonExport(exports, _jsonSkippedTypeNameSet))
        {
            if (Verbose)
                AppLog.Information("[SKIPPED]  {Prefix}{Path} (specialized export asset)", packageContext.Prefix, packageContext.Path);
            return;
        }

        var exportResult = PackageJsonExporter.TryExport(packageContext.Path, OutputDir, exports);
        if (exportResult.Failed)
        {
            LogFailure(packageContext, exportResult);
            return;
        }

        foreach (var exportedArtifact in exportResult.ExportedArtifacts)
            LogExport(packageContext, exportedArtifact);
    }

    internal static bool ShouldSkipJsonExport(IEnumerable<UObject> exports, IReadOnlySet<string> skippedTypeNames)
    {
        if (skippedTypeNames.Count == 0)
            return false;

        return exports.Any(export => skippedTypeNames.Contains(export.GetType().Name));
    }
}
