using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UnrealAssetScout.Incremental;

// One source's recorded facts, with every string replaced by an id into the manifest's interning
// tables. Read by ExportPlanner's staleness rules and rewritten by ManifestBuilder when a source
// is carried forward unchanged.
internal sealed class ManifestSource
{
    [JsonPropertyName("c")] public List<int> C { get; set; } = [];

    [JsonPropertyName("d")] public List<int> D { get; set; } = [];

    [JsonPropertyName("p")] public List<int> P { get; set; } = [];

    [JsonPropertyName("o")] public List<int> O { get; set; } = [];

    [JsonPropertyName("t")] public int? T { get; set; }

    [JsonPropertyName("u")] public int? U { get; set; }

    [JsonPropertyName("x")] public int? X { get; set; }

    [JsonPropertyName("e")] public bool E { get; set; }

    [JsonPropertyName("b")] public string B { get; set; } = BytecodeState.Unknown;

    [JsonPropertyName("s")] public string S { get; set; } = SourceStatus.Ok;

    [JsonPropertyName("ms")] public double Ms { get; set; }
}
