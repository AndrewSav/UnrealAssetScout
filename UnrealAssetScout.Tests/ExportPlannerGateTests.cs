using UnrealAssetScout.Incremental;

namespace UnrealAssetScout.Tests;

public sealed class ExportPlannerGateTests
{
    [Fact]
    public void Plan_NoManifest_RunsFullWithEverythingInTheWorkList()
    {
        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: null,
            sources: PlanInputsFixture.Sources("Game/A.uasset", "Game/B.uasset")));

        Assert.Null(result.Error);
        Assert.Equal(["Game/A.uasset", "Game/B.uasset"], result.Plan!.WorkList.Order());
        Assert.Empty(result.Plan.CarryForward);
        Assert.Null(result.Plan.Baseline);
    }

    [Fact]
    public void Plan_Rebuild_IgnoresManifestAndRunsFull()
    {
        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: PlanInputsFixture.Manifest("Game/A.uasset"),
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset"),
            rebuild: true));

        Assert.Null(result.Error);
        Assert.Equal(["Game/A.uasset"], result.Plan!.WorkList);
        Assert.Null(result.Plan.Baseline);
    }

    [Fact]
    public void Plan_Rebuild_CollapsesToolArrayToTheCurrentPair()
    {
        var manifest = PlanInputsFixture.Manifest("Game/A.uasset");
        manifest.Tool = [new ToolVersionPair(0, "old"), PlanInputsFixture.Tool];

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            rebuild: true));

        Assert.Equal([PlanInputsFixture.Tool], result.Plan!.ToolVersions);
    }

    [Fact]
    public void Plan_ModeMismatch_ErrorsAndNamesBothModes()
    {
        var manifest = PlanInputsFixture.Manifest("Game/A.uasset");
        manifest.Mode = "json";

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest, mode: "textures",
            sources: PlanInputsFixture.Sources("Game/A.uasset")));

        Assert.Null(result.Plan);
        Assert.Contains("'json'", result.Error);
        Assert.Contains("'textures'", result.Error);
        Assert.Contains("--rebuild", result.Error);
    }

    [Fact]
    public void Plan_GameMismatch_Errors()
    {
        var manifest = PlanInputsFixture.Manifest("Game/A.uasset");
        manifest.Game = "GAME_UE5_1";

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest, game: "GAME_UE5_4",
            sources: PlanInputsFixture.Sources("Game/A.uasset")));

        Assert.Null(result.Plan);
        Assert.Contains("GAME_UE5_4", result.Error);
    }

    [Fact]
    public void Plan_RecordedContainerNotMounted_Errors()
    {
        var manifest = PlanInputsFixture.Manifest("Game/A.uasset");
        manifest.Containers = ["a.pak", "pakchunk1-Windows.utoc"];

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest, containers: ["a.pak"],
            sources: PlanInputsFixture.Sources("Game/A.uasset")));

        Assert.Null(result.Plan);
        Assert.Contains("pakchunk1-Windows.utoc", result.Error);
        Assert.Contains("--paks", result.Error);
    }

    [Fact]
    public void Plan_NewlyMountedContainer_IsNotAnError()
    {
        var manifest = PlanInputsFixture.Manifest("Game/A.uasset");
        manifest.Containers = ["a.pak"];

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest, containers: ["a.pak", "b.pak"],
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset")));

        Assert.Null(result.Error);
    }

    [Fact]
    public void Plan_UnrecordedToolPair_Errors()
    {
        var manifest = PlanInputsFixture.Manifest("Game/A.uasset");
        manifest.Tool = [new ToolVersionPair(1, "OLD")];

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/A.uasset")));

        Assert.Null(result.Plan);
        Assert.Contains("OLD", result.Error);
        Assert.Contains("bbb", result.Error);
        Assert.Contains("--accept-tool-version", result.Error);
    }

    [Fact]
    public void Plan_UnrecordedToolPairAccepted_ProceedsAndAppendsInvalidatingNothing()
    {
        var manifest = PlanInputsFixture.Manifest("Game/A.uasset");
        var oldPair = new ToolVersionPair(1, "OLD");
        manifest.Tool = [oldPair];

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset"),
            acceptToolVersion: true));

        Assert.Null(result.Error);
        Assert.Empty(result.Plan!.WorkList);
        Assert.Equal(["Game/A.uasset"], result.Plan.CarryForward);
        Assert.Equal([oldPair, PlanInputsFixture.Tool], result.Plan.ToolVersions);
    }

    [Fact]
    public void Plan_RecordedToolPair_IsSilentAndMovesItToTheEnd()
    {
        var manifest = PlanInputsFixture.Manifest("Game/A.uasset");
        var otherPair = new ToolVersionPair(2, "ddd");
        manifest.Tool = [PlanInputsFixture.Tool, otherPair];

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset")));

        Assert.Null(result.Error);
        Assert.Equal([otherPair, PlanInputsFixture.Tool], result.Plan!.ToolVersions);
    }

    [Fact]
    public void Plan_DowngradeToRecordedToolPair_IsSilent()
    {
        var manifest = PlanInputsFixture.Manifest("Game/A.uasset");
        var newerPair = new ToolVersionPair(3, "zzz");
        manifest.Tool = [PlanInputsFixture.Tool, newerPair];

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset")));

        Assert.Null(result.Error);
        Assert.Equal([newerPair, PlanInputsFixture.Tool], result.Plan!.ToolVersions);
    }
}
