namespace UnrealAssetScout.Incremental;

// Which rule first marked a source stale, for the plan summary a run prints.
// Produced by ExportPlanner's direct staleness check and counted into PlanStatistics.
// The rules are evaluated in the order listed here and the first match wins, so a source stale for
// several reasons is attributed only to the earliest: the counts answer "why was this rebuilt",
// not "everything that was wrong with it", and evaluating the rest would cost a second pass over
// the slowest phase of PLAN for no decision.
internal enum StaleReason
{
    None,
    NewSource,
    ConstituentSetChanged,
    ConstituentContentChanged,
    DependencyChanged,
    OutputMissing,
    ExternalMedia,
    BytecodeFlagFlipped,
    SkipListChanged,
    UsmapTypeChanged,
    BlockedNameNowKnown,
    Propagated
}
