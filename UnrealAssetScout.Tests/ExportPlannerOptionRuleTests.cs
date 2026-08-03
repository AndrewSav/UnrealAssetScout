using UnrealAssetScout.Incremental;

namespace UnrealAssetScout.Tests;

public sealed class ExportPlannerOptionRuleTests
{
    // One source whose single export is a UTexture2D deriving from UTexture deriving from UObject.
    private static ExportManifest ManifestWithTextureExport()
    {
        var manifest = PlanInputsFixture.Manifest("Game/A.uasset");
        manifest.ClrTypes = ["UTexture2D", "UTexture", "UObject"];
        manifest.ClrTypeSets = [[0]];
        manifest.ClrTypeChains = new Dictionary<int, List<int>>
        {
            [0] = [0, 1, 2],
            [1] = [1, 2],
            [2] = [2]
        };
        manifest.Sources[0].X = 0;
        return manifest;
    }

    [Fact]
    public void Plan_SkipSetUnchanged_DoesNotInvalidate()
    {
        var manifest = ManifestWithTextureExport();
        manifest.SkipTypes = ["UTexture2D"];
        manifest.Sources[0].S = SourceStatus.SkippedBySkipList;
        manifest.Sources[0].O = [];

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset"),
            skipTypes: ["UTexture2D"]));

        Assert.Empty(result.Plan!.WorkList);
    }

    [Fact]
    public void Plan_PredicateFlipsToNotSkipped_IsStale()
    {
        var manifest = ManifestWithTextureExport();
        manifest.SkipTypes = ["UTexture2D"];
        manifest.Sources[0].S = SourceStatus.SkippedBySkipList;
        manifest.Sources[0].O = [];

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset"),
            skipTypes: []));

        Assert.Equal(["Game/A.uasset"], result.Plan!.WorkList);
    }

    [Fact]
    public void Plan_PredicateFlipsToSkipped_IsStale()
    {
        var manifest = ManifestWithTextureExport();
        manifest.SkipTypes = [];

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset"),
            skipTypes: ["UTexture2D"]));

        Assert.Equal(["Game/A.uasset"], result.Plan!.WorkList);
    }

    [Fact]
    public void Plan_SkipSetChangedButPredicateDoesNot_DoesNotInvalidate()
    {
        var manifest = ManifestWithTextureExport();
        manifest.SkipTypes = ["UStaticMesh"];

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset"),
            skipTypes: ["USkeletalMesh"]));

        Assert.Empty(result.Plan!.WorkList);
    }

    [Fact]
    public void Plan_SkipListMatchesABaseType_SkipsTheWholeChain()
    {
        var manifest = ManifestWithTextureExport();
        manifest.SkipTypes = [];

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset"),
            skipTypes: ["UTexture"]));

        Assert.Equal(["Game/A.uasset"], result.Plan!.WorkList);
    }

    [Fact]
    public void Plan_OneExportUnspecialized_PackageIsNotSkipped()
    {
        var manifest = ManifestWithTextureExport();
        manifest.ClrTypes = ["UTexture2D", "UTexture", "UObject", "UDataTable"];
        manifest.ClrTypeSets = [[0, 3]];
        manifest.ClrTypeChains[3] = [3, 2];
        manifest.SkipTypes = [];

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset"),
            skipTypes: ["UTexture2D"]));

        Assert.Empty(result.Plan!.WorkList);
    }

    [Theory]
    [InlineData(BytecodeState.True, true)]
    [InlineData(BytecodeState.Unknown, true)]
    [InlineData(BytecodeState.False, false)]
    public void Plan_ScriptBytecodeFlippedOn_InvalidatesOnlyTrueAndUnknown(string recorded, bool expectStale)
    {
        var manifest = PlanInputsFixture.Manifest("Game/A.uasset");
        manifest.ScriptBytecode = false;
        manifest.Sources[0].B = recorded;

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset"),
            scriptBytecode: true));

        Assert.Equal(expectStale ? 1 : 0, result.Plan!.WorkList.Count);
    }

    [Theory]
    [InlineData(BytecodeState.True, true)]
    [InlineData(BytecodeState.Unknown, true)]
    [InlineData(BytecodeState.False, false)]
    public void Plan_ScriptBytecodeFlippedOff_InvalidatesOnlyTrueAndUnknown(string recorded, bool expectStale)
    {
        var manifest = PlanInputsFixture.Manifest("Game/A.uasset");
        manifest.ScriptBytecode = true;
        manifest.Sources[0].B = recorded;

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset"),
            scriptBytecode: false));

        Assert.Equal(expectStale ? 1 : 0, result.Plan!.WorkList.Count);
    }

    [Fact]
    public void Plan_ScriptBytecodeUnchanged_DoesNotInvalidateUnknown()
    {
        var manifest = PlanInputsFixture.Manifest("Game/A.uasset");
        manifest.ScriptBytecode = true;
        manifest.Sources[0].B = BytecodeState.Unknown;

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset"),
            scriptBytecode: true));

        Assert.Empty(result.Plan!.WorkList);
    }

    [Fact]
    public void Plan_ExternalWwiseFlagSet_IsUnconditionallyStale()
    {
        var manifest = PlanInputsFixture.Manifest("Game/A.uasset");
        manifest.Sources[0].E = true;

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset")));

        Assert.Equal(["Game/A.uasset"], result.Plan!.WorkList);
    }
}
