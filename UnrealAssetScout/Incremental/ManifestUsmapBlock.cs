using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UnrealAssetScout.Incremental;

// Per-type and per-enum semantic usmap fingerprints, keyed by ueTypes and ueEnums table ids.
// Populated from a UsmapSnapshot when the manifest is written and diffed against a fresh snapshot
// by ExportPlanner to build the changed-type set.
internal sealed class ManifestUsmapBlock
{
    [JsonPropertyName("types")] public Dictionary<int, string> Types { get; set; } = [];
    [JsonPropertyName("enums")] public Dictionary<int, string> Enums { get; set; } = [];
}
