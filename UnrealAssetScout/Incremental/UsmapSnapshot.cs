using System.Collections.Generic;

namespace UnrealAssetScout.Incremental;

// A whole usmap reduced to what invalidation needs: a semantic fingerprint per type and per enum,
// plus the reference graph used to expand a source's recorded types into its full closure.
// Built by UsmapFingerprints from CUE4Parse TypeMappings, consumed by ExportPlanner.
internal sealed class UsmapSnapshot
{
    internal required IReadOnlyDictionary<string, string> TypeFingerprints { get; init; }
    internal required IReadOnlyDictionary<string, string> EnumFingerprints { get; init; }
    internal required IReadOnlyDictionary<string, UsmapTypeNode> Types { get; init; }

    internal static UsmapSnapshot Empty { get; } = new()
    {
        TypeFingerprints = new Dictionary<string, string>(),
        EnumFingerprints = new Dictionary<string, string>(),
        Types = new Dictionary<string, UsmapTypeNode>()
    };
}
