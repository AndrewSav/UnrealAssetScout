namespace UnrealAssetScout.Incremental;

// Either a plan or the reason planning stopped. Returned by ExportPlanner.Plan and unwrapped by
// IncrementalRunner. A non-null Error is always fatal: the run exits non-zero and names the actual
// mismatch rather than silently escalating to a full rebuild.
internal sealed record PlanResult(ExportPlan? Plan, string? Error)
{
    internal static PlanResult Ok(ExportPlan plan) => new(plan, null);
    internal static PlanResult Failed(string error) => new(null, error);
}
