using System.Collections.Generic;

namespace UnrealAssetScout.Incremental;

// What PLAN decided: which sources must be exported, which carry their manifest entries forward
// unchanged, the manifest those entries come from, and the tool version list to write back.
// Produced by ExportPlanner and consumed by IncrementalRunner to drive EXECUTE and COMMIT.
// Orphans are deliberately absent: they can only be computed after EXECUTE.
internal sealed record ExportPlan(
    IReadOnlyList<string> WorkList,
    IReadOnlyList<string> CarryForward,
    ExportManifest? Baseline,
    IReadOnlyList<ToolVersionPair> ToolVersions,
    PlanStatistics Statistics)
{
    internal bool HasMixedToolVersions => ToolVersions.Count > 1;
}
