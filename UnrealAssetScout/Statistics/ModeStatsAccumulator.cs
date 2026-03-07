using System.Collections.Generic;

namespace UnrealAssetScout.Statistics;

// Counts successful mode-specific processing hits and keeps a per-key breakdown for later reporting.
// Owned by RunStatsAccumulator, configured by ExportProcessor for the active mode path, and built
// into ModeStats when RunStats is assembled.
internal sealed class ModeStatsAccumulator
{
    private string? _summaryLabel;
    private int _hitCount;
    private readonly Dictionary<string, int> _hitsByKey = [];

    internal void SetSummaryLabel(string summaryLabel) => _summaryLabel = summaryLabel;

    internal void RecordHit(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        _hitCount++;
        _hitsByKey[key] = _hitsByKey.GetValueOrDefault(key) + 1;
    }

    internal ModeStats? Build()
        => _hitCount > 0 && !string.IsNullOrWhiteSpace(_summaryLabel)
            ? new ModeStats(_summaryLabel, _hitCount, new Dictionary<string, int>(_hitsByKey))
            : null;
}
