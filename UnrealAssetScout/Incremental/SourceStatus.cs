namespace UnrealAssetScout.Incremental;

// The three values ManifestSource.S can hold. Written by SourceRecorder. It records the outcome
// for the manifest's own honesty, so a failed source is not misreported as having succeeded; it is
// not consulted by ExportPlanner, which has no special case for a failed source at all.
internal static class SourceStatus
{
    internal const string Ok = "ok";
    internal const string Failed = "failed";
    internal const string SkippedBySkipList = "skipped-by-skip-list";
}
