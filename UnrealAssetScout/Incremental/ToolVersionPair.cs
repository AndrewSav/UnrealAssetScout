using System.Text.Json.Serialization;

namespace UnrealAssetScout.Incremental;

// Identifies what a dump was produced by, in the only terms that can change its bytes: this build's
// export-behaviour number and the CUE4Parse commit compiled into it. Created by IncrementalRunner when
// building PlanInputs, compared by ExportPlanner's tool gate against the list of pairs the manifest
// records, and written back into that list by ManifestBuilder.
internal sealed record ToolVersionPair(
    [property: JsonPropertyName("export")] int Export,
    [property: JsonPropertyName("cue4parse")] string Cue4Parse);
