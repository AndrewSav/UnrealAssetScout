using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnrealAssetScout.Export;

namespace UnrealAssetScout.Incremental;

// Builds S, the set of sources a from-scratch run with these options would consider, by applying
// the same extension rules, --filter and type filter that ExportProcessor.ProcessFiles applies,
// then grouping package payloads into the package that owns them.
// Called by IncrementalRunner at the start of PLAN. Keeping this identical to what
// ExportProcessor iterates is what lets scope options need no invalidation rule of their own.
internal static class SourceSetBuilder
{
    private static readonly string[] PayloadExtensions = ["uexp", "ubulk", "uptnl"];

    internal static Dictionary<string, SourceCandidate> Build(
        IEnumerable<string> paths,
        ExportMode mode,
        Regex? filter,
        IReadOnlySet<string>? typeFilteredPaths)
    {
        var all = paths as IReadOnlyCollection<string> ?? paths.ToList();

        if (mode is ExportMode.Raw or ExportMode.Simple)
        {
            return all
                .Where(path => IsSource(path, mode))
                .Where(path => InScope(path, filter, typeFilteredPaths))
                .ToDictionary(path => path, path => new SourceCandidate(path, [path]));
        }

        var payloadsByStem = all
            .Where(path => PayloadExtensions.Contains(Extension(path), StringComparer.OrdinalIgnoreCase))
            .GroupBy(Stem, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        return all
            .Where(path => IsSource(path, mode))
            .Where(path => InScope(path, filter, typeFilteredPaths))
            .ToDictionary(
                path => path,
                path => new SourceCandidate(
                    path,
                    payloadsByStem.TryGetValue(Stem(path), out var payloads) ? [path, .. payloads] : [path]));
    }

    private static bool InScope(string path, Regex? filter, IReadOnlySet<string>? typeFilteredPaths) =>
        (filter is null || filter.IsMatch(path)) &&
        (typeFilteredPaths is null || typeFilteredPaths.Contains(path));

    // Mirrors the mode switch in ExportProcessor.ProcessFiles exactly. If that switch changes,
    // this must change with it or the incremental source set drifts from what is exported.
    private static bool IsSource(string path, ExportMode mode)
    {
        var extension = Extension(path);
        return mode switch
        {
            ExportMode.Raw => true,
            ExportMode.Simple => extension is not ("uasset" or "umap" or "uexp" or "ubulk" or "uptnl"),
            ExportMode.Verse => extension is "uasset",
            _ => extension is "uasset" or "umap"
        };
    }

    private static string Extension(string path) =>
        Path.GetExtension(path).TrimStart('.').ToLowerInvariant();

    private static string Stem(string path) =>
        path[..^Path.GetExtension(path).Length];
}
