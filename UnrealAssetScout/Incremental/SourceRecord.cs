using System.Collections.Generic;

namespace UnrealAssetScout.Incremental;

// One source's observed facts, in plain strings, before interning.
// Produced by SourceRecorder during EXECUTE as each source is exported, then handed to
// ManifestBuilder, which converts every string into an id in the new manifest's tables.
// Null set fields mean "not applicable to this mode", and stay null in the manifest.
internal sealed class SourceRecord
{
    public required string Path { get; init; }
    public List<string> Constituents { get; init; } = [];
    public List<string> Dependencies { get; init; } = [];
    public List<string> LayoutProviders { get; init; } = [];
    public List<string> Outputs { get; init; } = [];
    public List<string>? UsmapTypes { get; init; }
    public List<string>? UnknownTypes { get; init; }
    public List<string>? ClrTypes { get; init; }
    public Dictionary<string, List<string>>? ClrTypeChains { get; init; }
    public bool ExternalWwise { get; init; }
    public double Milliseconds { get; init; }
    public required string Bytecode { get; init; }
    public required string Status { get; init; }
}
