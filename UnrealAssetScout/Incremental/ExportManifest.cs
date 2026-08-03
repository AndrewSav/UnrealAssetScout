using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UnrealAssetScout.Incremental;

// Serializable description of everything a completed export run produced: the options it ran
// under, the tool versions that produced the outputs, interning tables, and one entry per source.
// Written by IncrementalRunner at the end of a run and read back by ExportPlanner on the next run
// to decide what is stale. It is always a complete description of the dump, never a delta.
internal sealed class ExportManifest
{
    [JsonPropertyName("schema")] public int Schema { get; set; } = 2;
    [JsonPropertyName("mode")] public string Mode { get; set; } = string.Empty;
    [JsonPropertyName("game")] public string Game { get; set; } = string.Empty;
    [JsonPropertyName("tool")] public List<ToolVersionPair> Tool { get; set; } = [];
    [JsonPropertyName("skipTypes")] public List<string> SkipTypes { get; set; } = [];
    [JsonPropertyName("scriptBytecode")] public bool ScriptBytecode { get; set; }
    [JsonPropertyName("containers")] public List<string> Containers { get; set; } = [];
    [JsonPropertyName("usmap")] public ManifestUsmapBlock Usmap { get; set; } = new();
    [JsonPropertyName("paths")] public List<string> Paths { get; set; } = [];
    [JsonPropertyName("outputs")] public List<string> Outputs { get; set; } = [];
    [JsonPropertyName("ueTypes")] public List<string> UeTypes { get; set; } = [];
    [JsonPropertyName("ueEnums")] public List<string> UeEnums { get; set; } = [];
    [JsonPropertyName("clrTypes")] public List<string> ClrTypes { get; set; } = [];
    [JsonPropertyName("typeSets")] public List<List<int>> TypeSets { get; set; } = [];
    [JsonPropertyName("clrTypeSets")] public List<List<int>> ClrTypeSets { get; set; } = [];
    [JsonPropertyName("clrTypeChains")] public Dictionary<int, List<int>> ClrTypeChains { get; set; } = [];
    [JsonPropertyName("fingerprints")] public Dictionary<int, string> Fingerprints { get; set; } = [];
    [JsonPropertyName("sources")] public Dictionary<int, ManifestSource> Sources { get; set; } = [];
}
