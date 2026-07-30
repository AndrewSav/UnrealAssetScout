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

        // All or nothing. A package is skipped only when every export is specialized; otherwise it is
        // written in full, INCLUDING its specialized exports.
        //
        // Writing it in full is what keeps the file internally consistent. CUE4Parse emits object references
        // as "{package}.{ExportIndex}" but never emits ExportIndex on the objects themselves, so the only way
        // a consumer can resolve a reference is by array position -- which holds only while the array is
        // every export in order. Omitting any export silently shifts everything after it, and a reference
        // then resolves to a plausible WRONG object rather than failing. Dropping 1 269 exports from
        // Palworld's PL_MainWorld5.umap made all 152 fast-travel points resolve to each other's components.
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

    // True only when every export is specialized. An empty skip list or an empty package is never skipped,
    // preserving the previous behaviour for those cases.
    internal static bool ShouldSkipJsonExport(IEnumerable<UObject> exports, IReadOnlySet<string> skippedTypeNames)
    {
        if (skippedTypeNames.Count == 0)
            return false;

        var exportList = exports as IReadOnlyCollection<UObject> ?? exports.ToList();
        if (exportList.Count == 0)
            return false;

        return exportList.All(export => IsSpecialized(export, skippedTypeNames));
    }

    // Matches the export's own type name OR any of its base type names, so a skip-list entry can name a
    // whole category as well as a concrete type. This means that listing UObject would skip all exports.
    private static bool IsSpecialized(UObject export, IReadOnlySet<string> skippedTypeNames)
    {
        for (var type = export.GetType(); type is not null && type != typeof(object); type = type.BaseType)
        {
            if (skippedTypeNames.Contains(type.Name))
                return true;
        }

        return false;
    }
}
