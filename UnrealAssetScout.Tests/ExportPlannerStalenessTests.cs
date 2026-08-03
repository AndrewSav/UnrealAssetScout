using UnrealAssetScout.Incremental;

namespace UnrealAssetScout.Tests;

public sealed class ExportPlannerStalenessTests
{
    [Fact]
    public void Plan_SourceAbsentFromManifest_IsStale()
    {
        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: PlanInputsFixture.Manifest("Game/A.uasset"),
            sources: PlanInputsFixture.Sources("Game/A.uasset", "Game/New.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset", "Game/New.uasset")));

        Assert.Equal(["Game/New.uasset"], result.Plan!.WorkList);
        Assert.Equal(["Game/A.uasset"], result.Plan.CarryForward);
    }

    [Fact]
    public void Plan_EverythingUnchanged_ExportsNothing()
    {
        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: PlanInputsFixture.Manifest("Game/A.uasset", "Game/B.uasset"),
            sources: PlanInputsFixture.Sources("Game/A.uasset", "Game/B.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset", "Game/B.uasset")));

        Assert.Empty(result.Plan!.WorkList);
        Assert.Equal(2, result.Plan.CarryForward.Count);
    }

    [Fact]
    public void Plan_ConstituentFingerprintDiffers_IsStale()
    {
        var fingerprints = PlanInputsFixture.Fingerprints("Game/A.uasset", "Game/B.uasset");
        fingerprints["Game/A.uasset"] = "patched";

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: PlanInputsFixture.Manifest("Game/A.uasset", "Game/B.uasset"),
            sources: PlanInputsFixture.Sources("Game/A.uasset", "Game/B.uasset"),
            fingerprints: fingerprints));

        Assert.Equal(["Game/A.uasset"], result.Plan!.WorkList);
    }

    [Fact]
    public void Plan_ConstituentAdded_IsStale()
    {
        var sources = PlanInputsFixture.Sources("Game/A.uasset");
        sources["Game/A.uasset"] = new SourceCandidate("Game/A.uasset", ["Game/A.uasset", "Game/A.ubulk"]);
        var fingerprints = PlanInputsFixture.Fingerprints("Game/A.uasset", "Game/A.ubulk");

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: PlanInputsFixture.Manifest("Game/A.uasset"),
            sources: sources,
            fingerprints: fingerprints));

        Assert.Equal(["Game/A.uasset"], result.Plan!.WorkList);
    }

    [Fact]
    public void Plan_ConstituentRemoved_IsStale()
    {
        var manifest = PlanInputsFixture.Manifest("Game/A.uasset");
        var bulkId = manifest.Paths.Count;
        manifest.Paths.Add("Game/A.ubulk");
        manifest.Fingerprints[bulkId] = "hash-of-Game/A.ubulk";
        manifest.Sources[0].C = [0, bulkId];

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset")));

        Assert.Equal(["Game/A.uasset"], result.Plan!.WorkList);
    }

    [Fact]
    public void Plan_TrackedOutputMissingFromDisk_IsStale()
    {
        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: PlanInputsFixture.Manifest("Game/A.uasset", "Game/B.uasset"),
            sources: PlanInputsFixture.Sources("Game/A.uasset", "Game/B.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset", "Game/B.uasset"),
            outputExists: output => output != "Game/A.uasset.json"));

        Assert.Equal(["Game/A.uasset"], result.Plan!.WorkList);
    }

    [Fact]
    public void Plan_DirectDependencyChanged_IsStale()
    {
        var manifest = PlanInputsFixture.Manifest("Game/A.uasset", "Game/Dep.uasset");
        manifest.Sources[0].D = [manifest.Paths.IndexOf("Game/Dep.uasset")];
        var fingerprints = PlanInputsFixture.Fingerprints("Game/A.uasset", "Game/Dep.uasset");
        fingerprints["Game/Dep.uasset"] = "patched";

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/A.uasset", "Game/Dep.uasset"),
            fingerprints: fingerprints));

        Assert.Equal(["Game/A.uasset", "Game/Dep.uasset"], result.Plan!.WorkList.Order());
    }

    [Fact]
    public void Plan_DependencyAbsentFromContainers_IsStale()
    {
        // A .wem that is a dependency but never a source, and has now been removed.
        var manifest = PlanInputsFixture.Manifest("Game/Bank.uasset");
        var wemId = manifest.Paths.Count;
        manifest.Paths.Add("Game/Sound.wem");
        manifest.Fingerprints[wemId] = "hash-of-Game/Sound.wem";
        manifest.Sources[0].D = [wemId];

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/Bank.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/Bank.uasset")));

        Assert.Equal(["Game/Bank.uasset"], result.Plan!.WorkList);
    }

    [Fact]
    public void Plan_UnchangedDependency_DoesNotInvalidate()
    {
        var manifest = PlanInputsFixture.Manifest("Game/A.uasset");
        var wemId = manifest.Paths.Count;
        manifest.Paths.Add("Game/Sound.wem");
        manifest.Fingerprints[wemId] = "hash-of-Game/Sound.wem";
        manifest.Sources[0].D = [wemId];

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset", "Game/Sound.wem")));

        Assert.Empty(result.Plan!.WorkList);
    }

    [Fact]
    public void Plan_FailedSourceUnchanged_IsNotRetried()
    {
        var manifest = PlanInputsFixture.Manifest("Game/A.uasset");
        manifest.Sources[0].S = SourceStatus.Failed;
        manifest.Sources[0].O = [];

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset")));

        Assert.Empty(result.Plan!.WorkList);
    }

    [Fact]
    public void Plan_SourceOutsideScope_IsNeitherWorkNorCarryForward()
    {
        // Narrowing the filter drops Game/B.uasset from S. It must not appear anywhere in the
        // plan; COMMIT is what turns its unclaimed outputs into orphans.
        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: PlanInputsFixture.Manifest("Game/A.uasset", "Game/B.uasset"),
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset", "Game/B.uasset")));

        Assert.Empty(result.Plan!.WorkList);
        Assert.Equal(["Game/A.uasset"], result.Plan.CarryForward);
    }
}
