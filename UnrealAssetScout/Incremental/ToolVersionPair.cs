using System.Text.Json.Serialization;

namespace UnrealAssetScout.Incremental;

// The uas and CUE4Parse version pair identifying one build of the tooling.
// Built from Utils.AppVersion, written into the manifest by IncrementalRunner, and compared by
// ExportPlanner's tool gate, so that a CUE4Parse submodule bump which silently changes export
// output is detected rather than ignored.
internal sealed record ToolVersionPair(
    [property: JsonPropertyName("uas")] string Uas,
    [property: JsonPropertyName("cue4parse")] string Cue4Parse);
