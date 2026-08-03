using System;
using System.Collections.Generic;

namespace UnrealAssetScout.Incremental;

// The planner's entire input surface, as plain data. Nothing here touches disk, the provider or
// CUE4Parse, which is what makes every invalidation rule testable without mounting a game.
// Assembled by IncrementalRunner and passed to ExportPlanner.Plan.
internal sealed record PlanInputs(
    ExportManifest? Manifest,
    string Mode,
    string Game,
    ToolVersionPair Tool,
    IReadOnlyList<string> Containers,
    IReadOnlyList<string> SkipTypes,
    bool ScriptBytecode,
    IReadOnlyDictionary<string, SourceCandidate> Sources,
    IReadOnlyDictionary<string, string> Fingerprints,
    UsmapSnapshot Usmap,
    Func<string, bool> OutputExists,
    bool Rebuild,
    bool AcceptToolVersion,
    Func<string, string?> ResolvePackagePath);
