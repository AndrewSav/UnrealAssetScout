using System.Collections.Generic;

namespace UnrealAssetScout.Incremental;

// One source in the current source set S, with the files that make it up.
// Produced by SourceSetBuilder from the mounted provider, consumed by ExportPlanner.
internal sealed record SourceCandidate(string Path, IReadOnlyList<string> Constituents);
