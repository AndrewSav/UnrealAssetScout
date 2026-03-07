using System;
using UnrealAssetScout.Package;

namespace UnrealAssetScout.Statistics;

// Accumulates per-file timing samples, optional usmap-required counts, and optional mode-specific
// hit counts for a run. Created and used within ExportProcessor.ProcessFiles; produces a RunStats
// value via Build() at the end of the processing loop.
internal sealed class RunStatsAccumulator
{
    private int _count;
    private double _meanMs;
    private double _m2Ms;
    private double _maxMs;
    private int? _usmapRequiredCount;

    internal RunStatsAccumulator(bool trackUsmapRequiredCount)
    {
        if (trackUsmapRequiredCount)
            _usmapRequiredCount = 0;
    }

    internal ModeStatsAccumulator ModeStats { get; } = new();

    public void AddSample(double elapsedMs)
    {
        _count++;
        var delta = elapsedMs - _meanMs;
        _meanMs += delta / _count;
        var delta2 = elapsedMs - _meanMs;
        _m2Ms += delta * delta2;
        if (elapsedMs > _maxMs)
            _maxMs = elapsedMs;
    }

    internal void RecordRequirement(UsmapRequirement requirement)
    {
        if (_usmapRequiredCount.HasValue && requirement == UsmapRequirement.RequiresUsmap)
            _usmapRequiredCount++;
    }

    public RunStats Build()
    {
        var stdDev = _count > 0 ? Math.Sqrt(_m2Ms / _count) : 0d;
        return new RunStats(_count, _meanMs, stdDev, _maxMs, _usmapRequiredCount, ModeStats.Build());
    }
}
