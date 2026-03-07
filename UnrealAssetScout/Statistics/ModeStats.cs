using System.Collections.Generic;

namespace UnrealAssetScout.Statistics;

// Immutable summary of successful mode-specific processing hits grouped by key.
// Built by ModeStatsAccumulator at the end of ExportProcessor.ProcessFiles, stored in RunStats,
// and consumed by RuntimeReporting.WriteCompletionSummary.
internal readonly record struct ModeStats(
    string SummaryLabel,
    int TotalHitCount,
    IReadOnlyDictionary<string, int> HitsByKey);
