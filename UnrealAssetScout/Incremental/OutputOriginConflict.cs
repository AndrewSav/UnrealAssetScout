using System.Collections.Generic;
using UnrealAssetScout.Export;

namespace UnrealAssetScout.Incremental;

// One output file that more than one origin wrote, together with the origins that wrote it.
// Produced by DuplicateOutputCheck and rendered by IncrementalRunner, which names the origins
// rather than just the path, because knowing a file has two writers is only actionable once you
// can see which two.
internal sealed record OutputOriginConflict(string Output, IReadOnlyList<ArtifactOrigin> Origins);
