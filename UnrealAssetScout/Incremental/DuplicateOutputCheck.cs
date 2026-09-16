using System;
using System.Collections.Generic;
using System.Linq;
using UnrealAssetScout.Export;

namespace UnrealAssetScout.Incremental;

// Detects output files that two different origins write to, which means the bytes on disk depend on
// the order the run took rather than on the input. Several artifacts may share one output when they
// came from the same place: audio media referenced by more than one event is one file, not a clash.
// Origins are compared rather than content, because two pak entries holding equal bytes today are
// still separate data and nothing keeps them in step.
// Called by IncrementalRunner during COMMIT, once every source has reported what it produced.
internal static class DuplicateOutputCheck
{
    internal static IReadOnlyList<OutputOriginConflict> FindOutputsWithMoreThanOneOrigin(
        IEnumerable<ExportedArtifact> artifacts) =>
        artifacts
            .GroupBy(artifact => artifact.OutputPath, StringComparer.OrdinalIgnoreCase)
            .Select(written => new OutputOriginConflict(
                written.Key,
                written
                    .Select(artifact => artifact.OriginOrExport)
                    .GroupBy(origin => origin.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(sameOrigin => sameOrigin.First())
                    .OrderBy(origin => origin.Key, StringComparer.Ordinal)
                    .ToList()))
            .Where(conflict => conflict.Origins.Count > 1)
            .OrderBy(conflict => conflict.Output, StringComparer.Ordinal)
            .ToList();
}
