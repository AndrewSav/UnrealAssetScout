using System.Collections.Generic;
using System.Linq;

namespace UnrealAssetScout.Incremental;

// What a plan decided, counted for the summary lines a run prints.
// Produced by ExportPlanner alongside the plan itself and rendered by IncrementalRunner.
// UpdateCost is summed from the times the previous run recorded, so it answers "how long did this
// work take last time" rather than predicting the current run; sources the previous manifest has
// no time for are counted separately rather than silently lowering the total.
internal sealed record PlanStatistics(
    int Added,
    int Updated,
    int Unchanged,
    double UpdateCostMilliseconds,
    int UpdatesWithoutRecordedCost,
    IReadOnlyDictionary<StaleReason, int> Reasons)
{
    internal static PlanStatistics ForFullRun(int sourceCount) =>
        new(sourceCount, 0, 0, 0, 0, new Dictionary<StaleReason, int>());

    // Non-zero reasons only, in rule evaluation order, so the line names what actually fired.
    internal string DescribeReasons() =>
        string.Join(
            " | ",
            Reasons.Where(pair => pair.Value > 0)
                .OrderBy(pair => pair.Key)
                .Select(pair => $"{Label(pair.Key)} {pair.Value:N0}"));

    private static string Label(StaleReason reason) => reason switch
    {
        StaleReason.NewSource => "new source",
        StaleReason.ConstituentSetChanged => "constituent added or removed",
        StaleReason.ConstituentContentChanged => "constituent content changed",
        StaleReason.DependencyChanged => "dependency changed",
        StaleReason.OutputMissing => "output missing",
        StaleReason.ExternalMedia => "external media",
        StaleReason.BytecodeFlagFlipped => "script bytecode flag flipped",
        StaleReason.SkipListChanged => "skip list changed",
        StaleReason.UsmapTypeChanged => "usmap type changed",
        StaleReason.BlockedNameNowKnown => "blocked name now known",
        StaleReason.Propagated => "propagated from another source",
        _ => reason.ToString()
    };
}
