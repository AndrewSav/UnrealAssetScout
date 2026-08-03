using UnrealAssetScout.Incremental;

namespace UnrealAssetScout.Tests;

public sealed class ExportPlannerDependencyTests
{
    // A manifest whose single source depends on the UE package name "/Game/Dep", recorded as
    // resolved (with a fingerprint) or unresolved (without one).
    private static ExportManifest ManifestDependingOnName(bool recordedAsResolved)
    {
        var manifest = PlanInputsFixture.Manifest("Game/A.uasset");
        var depId = manifest.Paths.Count;
        manifest.Paths.Add("/Game/Dep");
        if (recordedAsResolved)
            manifest.Fingerprints[depId] = "hash-of-Game/Dep.uasset";

        manifest.Sources[0].D = [depId];
        return manifest;
    }

    [Fact]
    public void Plan_UnresolvedImportStillUnresolved_DoesNotInvalidate()
    {
        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: ManifestDependingOnName(recordedAsResolved: false),
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset"),
            resolvePackagePath: _ => null));

        Assert.Empty(result.Plan!.WorkList);
    }

    [Fact]
    public void Plan_UnresolvedImportNowResolves_IsStale()
    {
        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: ManifestDependingOnName(recordedAsResolved: false),
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset", "Game/Dep.uasset"),
            resolvePackagePath: name => name == "/Game/Dep" ? "Game/Dep.uasset" : null));

        Assert.Equal(["Game/A.uasset"], result.Plan!.WorkList);
    }

    [Fact]
    public void Plan_ResolvedImportNowUnresolved_IsStale()
    {
        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: ManifestDependingOnName(recordedAsResolved: true),
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset"),
            resolvePackagePath: _ => null));

        Assert.Equal(["Game/A.uasset"], result.Plan!.WorkList);
    }

    [Fact]
    public void Plan_ResolvedImportUnchanged_DoesNotInvalidate()
    {
        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: ManifestDependingOnName(recordedAsResolved: true),
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset", "Game/Dep.uasset"),
            resolvePackagePath: name => name == "/Game/Dep" ? "Game/Dep.uasset" : null));

        Assert.Empty(result.Plan!.WorkList);
    }

    [Fact]
    public void Plan_ResolvedImportContentChanged_IsStale()
    {
        var fingerprints = PlanInputsFixture.Fingerprints("Game/A.uasset", "Game/Dep.uasset");
        fingerprints["Game/Dep.uasset"] = "patched";

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: ManifestDependingOnName(recordedAsResolved: true),
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: fingerprints,
            resolvePackagePath: name => name == "/Game/Dep" ? "Game/Dep.uasset" : null));

        Assert.Equal(["Game/A.uasset"], result.Plan!.WorkList);
    }

    [Fact]
    public void Plan_PropagationFollowsNameEdgesToTheResolvedSource()
    {
        // Import-edge propagation survives only in the conversion modes, so identity resolution
        // inside Propagate is exercised there.
        var manifest = ManifestDependingOnName(recordedAsResolved: true);
        manifest.Mode = "models";
        // Game/Dep.uasset is itself a source, directly stale for a reason unrelated to its own
        // fingerprint (E, an external-Wwise marker) rather than a content change. If it were stale
        // via a fingerprint delta instead, Game/A.uasset's own dependency comparison would already
        // catch it directly, and this test would pass even if Propagate never resolved the edge.
        var depId = manifest.Paths.Count;
        manifest.Paths.Add("Game/Dep.uasset");
        var depOutputId = manifest.Outputs.Count;
        manifest.Outputs.Add("Game/Dep.uasset.json");
        manifest.Fingerprints[depId] = "hash-of-Game/Dep.uasset";
        manifest.Sources[depId] = new ManifestSource
        {
            C = [depId], O = [depOutputId], B = BytecodeState.False, S = SourceStatus.Ok, E = true
        };

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/A.uasset", "Game/Dep.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset", "Game/Dep.uasset"),
            mode: "models",
            resolvePackagePath: name => name == "/Game/Dep" ? "Game/Dep.uasset" : null));

        Assert.Equal(["Game/A.uasset", "Game/Dep.uasset"], result.Plan!.WorkList.Order());
    }

    [Fact]
    public void Plan_PackageIdentityDependencyUnchanged_DoesNotInvalidate()
    {
        var manifest = PlanInputsFixture.Manifest("Game/A.uasset");
        var depId = manifest.Paths.Count;
        manifest.Paths.Add("packageid:12345");
        manifest.Fingerprints[depId] = "hash-of-Game/Dep.uasset";
        manifest.Sources[0].D = [depId];

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset", "Game/Dep.uasset"),
            resolvePackagePath: identity => identity == "packageid:12345" ? "Game/Dep.uasset" : null));

        Assert.Empty(result.Plan!.WorkList);
    }

    [Fact]
    public void Plan_PropagationFollowsPackageIdentityEdgesToTheResolvedSource()
    {
        // Import-edge propagation survives only in the conversion modes, so identity resolution
        // inside Propagate is exercised there.
        var manifest = PlanInputsFixture.Manifest("Game/A.uasset");
        manifest.Mode = "models";
        var depId = manifest.Paths.Count;
        manifest.Paths.Add("packageid:12345");
        manifest.Sources[0].D = [depId];

        var targetId = manifest.Paths.Count;
        manifest.Paths.Add("Game/Dep.uasset");
        var targetOutputId = manifest.Outputs.Count;
        manifest.Outputs.Add("Game/Dep.uasset.json");
        manifest.Fingerprints[targetId] = "hash-of-Game/Dep.uasset";
        manifest.Sources[targetId] = new ManifestSource
        {
            C = [targetId], O = [targetOutputId], B = BytecodeState.False, S = SourceStatus.Ok
        };

        // Game/Dep.uasset's current fingerprint is left out of the live index below, so it is
        // directly stale on its own (an absent current fingerprint counts as changed), and the
        // packageid identity's own recorded-versus-current comparison sees absent on both sides too
        // -- it has no recorded fingerprint here, and "packageid:12345" is never a key in the live
        // index either. Game/A.uasset's own dependency comparison therefore cannot explain any
        // staleness by itself: only Propagate resolving the identity to Game/Dep.uasset's path can
        // sweep Game/A.uasset in.
        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/A.uasset", "Game/Dep.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset"),
            mode: "models",
            resolvePackagePath: identity => identity == "packageid:12345" ? "Game/Dep.uasset" : null));

        Assert.Equal(["Game/A.uasset", "Game/Dep.uasset"], result.Plan!.WorkList.Order());
    }

    [Fact]
    public void Plan_WwiseMediaDependencyIsTreatedAsAContainerPath()
    {
        var manifest = PlanInputsFixture.Manifest("Game/Bank.uasset");
        var wemId = manifest.Paths.Count;
        manifest.Paths.Add("Game/Sound.wem");
        manifest.Fingerprints[wemId] = "hash-of-Game/Sound.wem";
        manifest.Sources[0].D = [wemId];

        var fingerprints = PlanInputsFixture.Fingerprints("Game/Bank.uasset", "Game/Sound.wem");
        fingerprints["Game/Sound.wem"] = "patched";

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/Bank.uasset"),
            fingerprints: fingerprints,
            resolvePackagePath: _ => null));

        Assert.Equal(["Game/Bank.uasset"], result.Plan!.WorkList);
    }
}
