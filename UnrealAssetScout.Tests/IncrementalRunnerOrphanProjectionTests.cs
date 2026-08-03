using UnrealAssetScout.Incremental;

namespace UnrealAssetScout.Tests;

// Covers IncrementalRunner.ProjectOrphans without a live provider: it takes only an
// ExportManifest? and an ExportPlan, both plain data.
public sealed class IncrementalRunnerOrphanProjectionTests
{
    private static ExportPlan Plan(IReadOnlyList<string> carryForward, ExportManifest? baseline) =>
        new([], carryForward, baseline, [PlanInputsFixture.Tool], PlanStatistics.ForFullRun(0));

    [Fact]
    public void ProjectOrphans_NoPreviousManifest_ReturnsZero()
    {
        Assert.Equal(0, IncrementalRunner.ProjectOrphans(null, Plan([], null)));
    }

    [Fact]
    public void ProjectOrphans_EverythingCarriedForward_ReturnsZero()
    {
        var previous = PlanInputsFixture.Manifest("A", "B", "C");

        Assert.Equal(0, IncrementalRunner.ProjectOrphans(previous, Plan(["A", "B", "C"], previous)));
    }

    [Fact]
    public void ProjectOrphans_PartialCarryForward_CountsOnlyTheDroppedOutputs()
    {
        var previous = PlanInputsFixture.Manifest("A", "B", "C");

        Assert.Equal(2, IncrementalRunner.ProjectOrphans(previous, Plan(["A"], previous)));
    }

    // The regression this guards: ExportPlanner.Plan sets ExportPlan.Baseline to null whenever
    // Rebuild is true, regardless of whether a manifest already exists on disk. Projecting from
    // plan.Baseline instead of from the manifest actually loaded from disk would make
    // "--rebuild --dry-run" against an existing dump report zero deletions, right before a real
    // "--rebuild" run deletes every output the previous manifest tracked. Baseline is deliberately
    // null here, with a non-null previous manifest and an empty CarryForward, exactly what
    // ExportPlanner.Plan produces for Rebuild: true against an existing manifest.
    [Fact]
    public void ProjectOrphans_RebuildAgainstAnExistingManifest_CountsEveryPreviousOutput()
    {
        var previous = PlanInputsFixture.Manifest("A", "B", "C");

        Assert.Equal(3, IncrementalRunner.ProjectOrphans(previous, Plan([], baseline: null)));
    }
}
