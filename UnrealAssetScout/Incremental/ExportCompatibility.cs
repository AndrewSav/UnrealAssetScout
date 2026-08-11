namespace UnrealAssetScout.Incremental;

// The identity of this build's export behaviour, recorded in the manifest and compared by
// ExportPlanner's tool gate. Read by IncrementalRunner when building PlanInputs.
internal static class ExportCompatibility
{
    // Bump when a change can make an export produce different bytes for inputs that did not change:
    // an exporter, the skip-type defaults, naming or layout of outputs, or a CUE4Parse call whose
    // result is written out. Do not bump for a release, a refactor, or anything the exported bytes
    // cannot observe. Forgetting to bump carries stale outputs forward silently, which is why the
    // release checklist in CLAUDE.md asks about it.
    internal const int Version = 1;
}
