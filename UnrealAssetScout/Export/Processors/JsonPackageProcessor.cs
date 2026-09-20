using System;
using System.Collections.Generic;
using System.Linq;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Objects.UObject;
using UnrealAssetScout.Export.Exporters;
using UnrealAssetScout.Logging;
using UnrealAssetScout.Package;

namespace UnrealAssetScout.Export.Processors;

// Processes package exports for JSON mode.
// Created by ExportProcessor.ProcessFiles for ExportMode.Json, then passed to
// ExportProcessor.ProcessPackageMode to apply the JSON skip list and write package JSON output.
// The skip list is tried against the export map before the exports are loaded, so a package that
// is skipped in full never deserializes.
internal sealed class JsonPackageProcessor(string outputDir, bool verbose, IReadOnlyCollection<string> jsonSkipTypeNames) : PackageModeProcessorBase(outputDir, verbose, null)
{
    private readonly HashSet<string> _jsonSkippedTypeNameSet = new(jsonSkipTypeNames, StringComparer.OrdinalIgnoreCase);

    internal bool SkippedBySkipList { get; private set; }

    // Set only when the skip list was matched from the export map, so the exports were never
    // loaded. SourceRecorder needs these to record what ExportPlanner.SkipPredicate reads.
    internal IReadOnlyList<Type>? SkippedExportTypes { get; private set; }

    public override void ProcessPackage(PackageExportContext packageContext)
    {
        var package = packageContext.Package!;

        // Deserializing an export can cost far more than writing it would: an animation decompresses
        // every curve and bone key first. Deciding from the export map keeps a package that is
        // skipped in full from paying that for output nothing asked for.
        var exportTypes = ResolveExportTypes(EnumerateExportClassNames(package));
        if (ShouldSkip(exportTypes, _jsonSkippedTypeNameSet))
        {
            SkippedExportTypes = exportTypes;
            RecordSkipped(packageContext);
            return;
        }

        var exports = package.GetExports().ToList();

        // A package is skipped only when every export is specialized
        if (ShouldSkipJsonExport(exports, _jsonSkippedTypeNameSet))
        {
            RecordSkipped(packageContext);
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

    private void RecordSkipped(PackageExportContext packageContext)
    {
        SkippedBySkipList = true;
        if (Verbose)
            AppLog.Information("[SKIPPED]  {Prefix}{Path} (specialized export asset)", packageContext.Prefix, packageContext.Path);
    }

    // Class names are read from the export map rather than via UObject.ExportType, because the
    // latter forces the lazy export to deserialize, which is the cost this check exists to avoid.
    private static IEnumerable<string?> EnumerateExportClassNames(IPackage package)
    {
        for (var exportIndex = 0; exportIndex < package.ExportMapLength; exportIndex++)
        {
            var resolvedExport = package.ResolvePackageIndex(new FPackageIndex(package, exportIndex + 1));
            yield return resolvedExport?.Class?.Name.Text;
        }
    }

    internal static bool ShouldSkipByClassName(IEnumerable<string?> exportClassNames, IReadOnlySet<string> skippedTypeNames) =>
        ShouldSkip(ResolveExportTypes(exportClassNames), skippedTypeNames);

    // Null when any class name does not resolve. That defers to ShouldSkipJsonExport, which sees the
    // constructed object and so still matches an export whose class is a blueprint standing on a
    // skipped native parent.
    private static IReadOnlyList<Type>? ResolveExportTypes(IEnumerable<string?> exportClassNames)
    {
        var exportTypes = new List<Type>();
        foreach (var exportClassName in exportClassNames)
        {
            var exportType = exportClassName is null ? null : ObjectTypeRegistry.GetClass(exportClassName);
            if (exportType is null)
                return null;

            exportTypes.Add(exportType);
        }

        return exportTypes;
    }

    // True only when every export resolved to a code type on the skip list. An empty skip list or an
    // empty package is never skipped, matching ShouldSkipJsonExport.
    private static bool ShouldSkip(IReadOnlyList<Type>? exportTypes, IReadOnlySet<string> skippedTypeNames) =>
        skippedTypeNames.Count > 0 &&
        exportTypes is { Count: > 0 } &&
        exportTypes.All(exportType => IsSpecialized(exportType, skippedTypeNames));

    // True only when every export is specialized. An empty skip list or an empty package is never skipped,
    // preserving the previous behaviour for those cases.
    internal static bool ShouldSkipJsonExport(IEnumerable<UObject> exports, IReadOnlySet<string> skippedTypeNames)
    {
        if (skippedTypeNames.Count == 0)
            return false;

        var exportList = exports as IReadOnlyCollection<UObject> ?? exports.ToList();
        if (exportList.Count == 0)
            return false;

        return exportList.All(export => IsSpecialized(export.GetType(), skippedTypeNames));
    }

    // Matches the export's own type name OR any of its base type names, so a skip-list entry can name a
    // whole category as well as a concrete type. This means that listing UObject would skip all exports.
    private static bool IsSpecialized(Type exportType, IReadOnlySet<string> skippedTypeNames)
    {
        for (var type = exportType; type is not null && type != typeof(object); type = type.BaseType)
        {
            if (skippedTypeNames.Contains(type.Name))
                return true;
        }

        return false;
    }
}
