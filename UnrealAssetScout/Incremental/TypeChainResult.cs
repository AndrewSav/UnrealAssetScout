using System.Collections.Generic;

namespace UnrealAssetScout.Incremental;

// What a package's deserialization asked the usmap for, as produced by TypeChainWalker and
// recorded by SourceRecorder into `t` and `u`.
// Known holds the names the usmap answered: chain-end names whose layout or chain repair came
// from the mappings, and property references that did not resolve live. Stopped holds the names
// the usmap could not answer; if such a name appears in a later usmap, layout or repair becomes
// available and output can change, so `u` re-exports the package.
internal readonly record struct TypeChainResult(IReadOnlyList<string> Known, IReadOnlyList<string> Stopped);
