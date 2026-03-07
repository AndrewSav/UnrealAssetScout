using System.Collections.Generic;

namespace UnrealAssetScout.TypeFiltering;

// Describes the per-path export summary that type-filter expressions evaluate against.
// Produced from `list --format types` CSV files by TypeFilterSupport and consumed by
// TypeFilterParser predicates to decide whether a path matches a runtime type expression.
public sealed class PackageModel
{
    /// <summary>
    /// Package identifier.
    /// This is not used by the filter grammar itself, but is used
    /// to identify matching packages in the output.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// Total number of exports. Corresponds to %e / %exports.
    /// Must be >= 0.
    /// </summary>
    public int ExportCount { get; init; }

    /// <summary>
    /// Number of distinct export types. Corresponds to %t / %types.
    /// Must be >= 0.
    /// </summary>
    public int ExportTypeCount { get; init; }

    /// <summary>
    /// Per-type export counts, for example:
    /// { "js": 3, "css": 1 }
    /// Values should be >= 0.
    /// </summary>
    public IReadOnlyDictionary<string, int> TypeCounts { get; init; }
            = new Dictionary<string, int>();
}
