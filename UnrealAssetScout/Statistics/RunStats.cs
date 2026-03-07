namespace UnrealAssetScout.Statistics;

// Immutable record holding the final per-file timing statistics and optional mode-specific
// processing statistics for a run. Returned by ExportProcessor.ProcessFiles and printed by
// Program.Main on normal completion.
internal readonly record struct RunStats(
    int ProcessedFileCount,
    double AverageMilliseconds,
    double StandardDeviationMilliseconds,
    double MaximumMilliseconds,
    int? UsmapRequiredCount,
    ModeStats? ModeStats);
