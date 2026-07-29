using System.Collections.Generic;
using System.Linq;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Objects.UObject;
using UnrealAssetScout.Package;

namespace UnrealAssetScout.List;

// Formats the optional CSV export-composition rows shown by list mode.
// Called by ListProcessor after each matching file is inspected so list mode can emit stable
// `Path,Type,Count` rows for external analysis without duplicating grouping or CSV escaping logic.
// Export type names are read from the package export map rather than from deserialized export
// objects, because the class name is all these rows need.
internal static class ListExportSummaryFormatter
{
    internal static IReadOnlyList<string> FormatPackageExports(string path, PackageExportContext packageContext)
    {
        if (packageContext.LoadResult != PackageLoadResult.Success || packageContext.Package is null)
            return FormatNoExports(path);

        return FormatPackageExports(path, EnumerateExportTypeNames(packageContext.Package));
    }

    // Resolves each export map entry's class name. This mirrors what UObject.ExportType reports, but
    // without forcing the lazy export to deserialize: the class is resolved from the export's class
    // index, and an export whose class cannot be resolved is constructed as a plain UObject.
    private static IEnumerable<string> EnumerateExportTypeNames(IPackage package)
    {
        for (var exportIndex = 0; exportIndex < package.ExportMapLength; exportIndex++)
        {
            var resolvedExport = package.ResolvePackageIndex(new FPackageIndex(package, exportIndex + 1));
            yield return resolvedExport?.Class?.Name.Text ?? nameof(UObject);
        }
    }

    internal static IReadOnlyList<string> FormatPackageExports(string path, IEnumerable<string> exportTypeNames)
    {
        var exportTypeCounts = new SortedDictionary<string, int>(System.StringComparer.Ordinal);
        foreach (var exportTypeName in exportTypeNames)
        {
            exportTypeCounts.TryGetValue(exportTypeName, out var existingCount);
            exportTypeCounts[exportTypeName] = existingCount + 1;
        }

        if (exportTypeCounts.Count == 0)
            return FormatNoExports(path);

        return
        [
            .. exportTypeCounts.Select(pair => FormatRow(path, pair.Key, pair.Value))
        ];
    }

    internal static IReadOnlyList<string> FormatNoExports(string path) =>
    [
        FormatRow(path, string.Empty, null)
    ];

    private static string FormatRow(string path, string type, int? count) =>
        string.Join(",",
            EscapeCsvField(path),
            EscapeCsvField(type),
            count?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);

    private static string EscapeCsvField(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\r') && !value.Contains('\n'))
            return value;

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
