using UnrealAssetScout.Incremental;

namespace UnrealAssetScout.Tests;

public sealed class ExportPlannerStatisticsTests
{
    [Fact]
    public void Plan_CountsAdditionsUpdatesAndUnchangedSeparately()
    {
        var manifest = PlanInputsFixture.Manifest("Game/Changed.uasset", "Game/Same.uasset");
        manifest.Sources[manifest.Paths.IndexOf("Game/Changed.uasset")].Ms = 100;
        manifest.Sources[manifest.Paths.IndexOf("Game/Same.uasset")].Ms = 50;

        var fingerprints = PlanInputsFixture.Fingerprints(
            "Game/Changed.uasset", "Game/Same.uasset", "Game/New.uasset");
        fingerprints["Game/Changed.uasset"] = "patched";

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/Changed.uasset", "Game/Same.uasset", "Game/New.uasset"),
            fingerprints: fingerprints));

        var statistics = result.Plan!.Statistics;
        Assert.Equal(1, statistics.Added);
        Assert.Equal(1, statistics.Updated);
        Assert.Equal(1, statistics.Unchanged);
        // Only the updated source's recorded time, never the unchanged one's and never the
        // addition, which has never been exported and so has no time at all.
        Assert.Equal(100, statistics.UpdateCostMilliseconds);
        Assert.Equal(0, statistics.UpdatesWithoutRecordedCost);
    }

    [Fact]
    public void Plan_CountsUpdatesThatHaveNoRecordedTimeRatherThanTreatingThemAsFree()
    {
        var manifest = PlanInputsFixture.Manifest("Game/A.uasset");
        var fingerprints = PlanInputsFixture.Fingerprints("Game/A.uasset");
        fingerprints["Game/A.uasset"] = "patched";

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: fingerprints));

        Assert.Equal(0, result.Plan!.Statistics.UpdateCostMilliseconds);
        Assert.Equal(1, result.Plan.Statistics.UpdatesWithoutRecordedCost);
    }

    [Fact]
    public void Plan_AttributesEachStaleSourceToTheFirstRuleThatFires()
    {
        // The source is stale for two reasons at once: its own bytes changed and the bytecode
        // flag flipped. Attribution names the earlier rule, not both.
        var manifest = PlanInputsFixture.Manifest("Game/A.uasset");
        manifest.ScriptBytecode = false;
        manifest.Sources[0].B = BytecodeState.True;
        var fingerprints = PlanInputsFixture.Fingerprints("Game/A.uasset");
        fingerprints["Game/A.uasset"] = "patched";

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: fingerprints,
            scriptBytecode: true));

        var reasons = result.Plan!.Statistics.Reasons;
        Assert.Equal(1, reasons[StaleReason.ConstituentContentChanged]);
        Assert.False(reasons.ContainsKey(StaleReason.BytecodeFlagFlipped));
    }

    [Fact]
    public void Plan_CountsPropagatedSourcesApartFromDirectlyStaleOnes()
    {
        var paths = new[] { "Game/Provider.uasset", "Game/Consumer.uasset" };
        var manifest = PlanInputsFixture.Manifest(paths);
        var stemId = manifest.Paths.Count;
        manifest.Paths.Add("Game/Provider");
        manifest.Sources[manifest.Paths.IndexOf("Game/Consumer.uasset")].P = [stemId];

        var fingerprints = PlanInputsFixture.Fingerprints(paths);
        fingerprints["Game/Provider.uasset"] = "patched";

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest, sources: PlanInputsFixture.Sources(paths), fingerprints: fingerprints));

        var reasons = result.Plan!.Statistics.Reasons;
        Assert.Equal(1, reasons[StaleReason.ConstituentContentChanged]);
        Assert.Equal(1, reasons[StaleReason.Propagated]);
        Assert.Equal(2, result.Plan.Statistics.Updated);
    }

    [Fact]
    public void DescribeReasons_NamesOnlyTheRulesThatFired()
    {
        var statistics = new PlanStatistics(0, 3, 0, 0, 0, new Dictionary<StaleReason, int>
        {
            [StaleReason.OutputMissing] = 0,
            [StaleReason.Propagated] = 2,
            [StaleReason.NewSource] = 1
        });

        // Rule evaluation order, zero counts omitted.
        Assert.Equal("new source 1 | propagated from another source 2", statistics.DescribeReasons());
    }
}
