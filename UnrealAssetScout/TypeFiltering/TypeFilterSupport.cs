using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using UnrealAssetScout.Logging;

namespace UnrealAssetScout.TypeFiltering;

// Loads `list --format types` CSV files, evaluates a type-filter expression against the reconstructed
// per-path PackageModel values, and returns the matching path set for Program/Main to apply on top
// of the normal regex filter in both list and export runs.
internal static class TypeFilterSupport
{
    internal static bool TryGetTypeFilteredPaths(
        Func<PackageModel, bool> predicate,
        string typesFilePath,
        out HashSet<string>? matchingPaths)
    {
        matchingPaths = null;
        try
        {
            var packages = LoadTypeInfo(typesFilePath);
            matchingPaths = packages
                .Where(predicate)
                .Select(static package => package.Path)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException or CsvHelperException)
        {
            AppLog.Error("Failed to load type filter CSV '{Path}': {Message}", typesFilePath, e.Message);
            return false;
        }
    }

    internal static IReadOnlyList<PackageModel> LoadTypeInfo(string typesFilePath)
    {
        using var reader = new StreamReader(typesFilePath);
        if (reader.Peek() < 0)
            throw new FormatException("The CSV file is empty.");

        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            DetectColumnCountChanges = true,
            IgnoreBlankLines = true,
            TrimOptions = TrimOptions.None
        });

        return csv.GetRecords<TypeSummaryRow>()
            .GroupBy(static row => row.Path, StringComparer.OrdinalIgnoreCase)
            .Select(static group =>
            {
                var typeCounts = group
                    .Where(static row => !string.IsNullOrEmpty(row.Type))
                    .GroupBy(static row => row.Type!, StringComparer.Ordinal)
                    .ToDictionary(
                        static g => g.Key,
                        static g => g.Sum(static row => row.Count!.Value),
                        StringComparer.Ordinal);

                return new PackageModel
                {
                    Path = group.Key,
                    ExportCount = typeCounts.Values.Sum(),
                    ExportTypeCount = typeCounts.Count,
                    TypeCounts = typeCounts
                };
            })
            .OrderBy(static package => package.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    // ReSharper disable once ClassNeverInstantiated.Local
    private sealed class TypeSummaryRow
    {
        // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
        public string Path { get; init; } = string.Empty;
        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        public string? Type { get; init; }
        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        public int? Count { get; init; }
    }
}
