using UnrealAssetScout.Incremental;

namespace UnrealAssetScout.Tests;

public sealed class ExportPlannerPropagationTests
{
    // Builds a manifest over the given paths and wires layout-provider edges by path name; the
    // provider is stored the way SourceRecorder records it, as a stem without an extension.
    private static ExportManifest ManifestWithLayoutEdges(
        string[] paths, params (string From, string To)[] edges)
    {
        var manifest = PlanInputsFixture.Manifest(paths);
        foreach (var (from, to) in edges)
        {
            var stem = to.Substring(0, to.LastIndexOf('.'));
            var stemId = manifest.Paths.IndexOf(stem);
            if (stemId < 0)
            {
                stemId = manifest.Paths.Count;
                manifest.Paths.Add(stem);
            }

            manifest.Sources[manifest.Paths.IndexOf(from)].P.Add(stemId);
        }

        return manifest;
    }

    private static ExportManifest ManifestWithImportEdges(
        string[] paths, params (string From, string To)[] edges)
    {
        var manifest = PlanInputsFixture.Manifest(paths);
        foreach (var (from, to) in edges)
            manifest.Sources[manifest.Paths.IndexOf(from)].D.Add(manifest.Paths.IndexOf(to));

        return manifest;
    }

    [Fact]
    public void Plan_ChangeReachesLayoutConsumersAtTwoHops()
    {
        var paths = new[] { "Game/A.uasset", "Game/B.uasset", "Game/C.uasset", "Game/Unrelated.uasset" };
        var manifest = ManifestWithLayoutEdges(paths, ("Game/A.uasset", "Game/B.uasset"), ("Game/B.uasset", "Game/C.uasset"));
        var fingerprints = PlanInputsFixture.Fingerprints(paths);
        fingerprints["Game/C.uasset"] = "patched";

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest, sources: PlanInputsFixture.Sources(paths), fingerprints: fingerprints));

        Assert.Equal(["Game/A.uasset", "Game/B.uasset", "Game/C.uasset"], result.Plan!.WorkList.Order());
        Assert.Equal(["Game/Unrelated.uasset"], result.Plan.CarryForward);
    }

    [Fact]
    public void Plan_PlainImportEdgesDoNotPropagateBeyondTheDirectDependencyRule()
    {
        // C changes; B imports C, so B is stale through its own dependency comparison. A imports
        // B, but B's bytes did not change and A took no layout from anything, so the staleness
        // must stop at B: a plain reference stores only a name, and a name does not change.
        var paths = new[] { "Game/A.uasset", "Game/B.uasset", "Game/C.uasset" };
        var manifest = ManifestWithImportEdges(paths, ("Game/A.uasset", "Game/B.uasset"), ("Game/B.uasset", "Game/C.uasset"));
        var fingerprints = PlanInputsFixture.Fingerprints(paths);
        fingerprints["Game/C.uasset"] = "patched";

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest, sources: PlanInputsFixture.Sources(paths), fingerprints: fingerprints));

        Assert.Equal(["Game/B.uasset", "Game/C.uasset"], result.Plan!.WorkList.Order());
        Assert.Equal(["Game/A.uasset"], result.Plan.CarryForward);
    }

    [Fact]
    public void Plan_ConverterEmbeddingModeStillPropagatesOverImportEdges()
    {
        // An animation's converted output embeds its skeleton, which is a plain import, not a
        // recorded layout provider. The conversion modes therefore keep import-edge propagation.
        var paths = new[] { "Game/A.uasset", "Game/B.uasset" };
        var manifest = ManifestWithImportEdges(paths, ("Game/A.uasset", "Game/B.uasset"));
        manifest.Mode = "animations";
        var fingerprints = PlanInputsFixture.Fingerprints(paths);
        fingerprints["Game/B.uasset"] = "patched";

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest, sources: PlanInputsFixture.Sources(paths), fingerprints: fingerprints,
            mode: "animations"));

        Assert.Equal(["Game/A.uasset", "Game/B.uasset"], result.Plan!.WorkList.Order());
    }

    [Fact]
    public void Plan_MutualLayoutCycleTerminates()
    {
        var paths = new[] { "Game/A.uasset", "Game/B.uasset" };
        var manifest = ManifestWithLayoutEdges(paths, ("Game/A.uasset", "Game/B.uasset"), ("Game/B.uasset", "Game/A.uasset"));
        var fingerprints = PlanInputsFixture.Fingerprints(paths);
        fingerprints["Game/A.uasset"] = "patched";

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest, sources: PlanInputsFixture.Sources(paths), fingerprints: fingerprints));

        Assert.Equal(["Game/A.uasset", "Game/B.uasset"], result.Plan!.WorkList.Order());
    }

    [Fact]
    public void Plan_PropagationDoesNotFlowForwardAlongLayoutEdges()
    {
        // A embeds layout from B. B changing invalidates A. A changing must NOT invalidate B.
        var paths = new[] { "Game/A.uasset", "Game/B.uasset" };
        var manifest = ManifestWithLayoutEdges(paths, ("Game/A.uasset", "Game/B.uasset"));
        var fingerprints = PlanInputsFixture.Fingerprints(paths);
        fingerprints["Game/A.uasset"] = "patched";

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest, sources: PlanInputsFixture.Sources(paths), fingerprints: fingerprints));

        Assert.Equal(["Game/A.uasset"], result.Plan!.WorkList);
    }

    [Fact]
    public void Plan_ImportThatPreviouslyFailedToResolveAndNowExists_InvalidatesTheImporter()
    {
        // A recorded an unresolved import of Game/New.uasset. The patch added it, so A's class
        // chain now reaches further even though A's own bytes did not change.
        var manifest = PlanInputsFixture.Manifest("Game/A.uasset");
        var newId = manifest.Paths.Count;
        manifest.Paths.Add("Game/New.uasset");
        manifest.Sources[0].D = [newId];

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/A.uasset", "Game/New.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset", "Game/New.uasset")));

        Assert.Equal(["Game/A.uasset", "Game/New.uasset"], result.Plan!.WorkList.Order());
    }

    [Fact]
    public void Plan_ChangedOutOfScopeDependencyInvalidatesItsImporterDirectly()
    {
        // The changed package is outside the current source set, so it can never seed the walk
        // itself; only A's own recorded-versus-current dependency comparison can catch this.
        var paths = new[] { "Game/A.uasset", "Game/OutOfScope.uasset" };
        var manifest = ManifestWithImportEdges(paths, ("Game/A.uasset", "Game/OutOfScope.uasset"));
        var fingerprints = PlanInputsFixture.Fingerprints(paths);
        fingerprints["Game/OutOfScope.uasset"] = "patched";

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: fingerprints));

        Assert.Equal(["Game/A.uasset"], result.Plan!.WorkList);
    }

    [Fact]
    public void Plan_LayoutProviderStemMatchesTheSourcePathCaseInsensitively()
    {
        // This application always configures the provider with StringComparer.OrdinalIgnoreCase,
        // so a recorded provider name can differ in case from the path the source is keyed under.
        // The stem index Propagate builds must tolerate that or it silently loses the edge --
        // under-invalidation rather than the safe direction.
        var manifest = PlanInputsFixture.Manifest("Game/A.uasset", "Game/dep.uasset");
        var stemId = manifest.Paths.Count;
        manifest.Paths.Add("Game/DEP");
        manifest.Sources[manifest.Paths.IndexOf("Game/A.uasset")].P = [stemId];

        var fingerprints = PlanInputsFixture.Fingerprints("Game/A.uasset", "Game/dep.uasset");
        fingerprints["Game/dep.uasset"] = "patched";

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/A.uasset", "Game/dep.uasset"),
            fingerprints: fingerprints));

        Assert.Equal(["Game/A.uasset", "Game/dep.uasset"], result.Plan!.WorkList.Order());
    }

    [Fact]
    public void Plan_PackageNameFormLayoutProviderResolvesThroughThePackagePathResolver()
    {
        // IoStore packages report their owner under the UE package name form, so a recorded
        // provider can be "/Game/Dep" rather than a path stem.
        var manifest = PlanInputsFixture.Manifest("Game/A.uasset", "Game/dep.uasset");
        var identityId = manifest.Paths.Count;
        manifest.Paths.Add("/Game/Dep");
        manifest.Sources[manifest.Paths.IndexOf("Game/A.uasset")].P = [identityId];

        var fingerprints = PlanInputsFixture.Fingerprints("Game/A.uasset", "Game/dep.uasset");
        fingerprints["Game/dep.uasset"] = "patched";

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/A.uasset", "Game/dep.uasset"),
            fingerprints: fingerprints,
            resolvePackagePath: identity => identity == "/Game/Dep" ? "Game/dep.uasset" : null));

        Assert.Equal(["Game/A.uasset", "Game/dep.uasset"], result.Plan!.WorkList.Order());
    }

    [Fact]
    public void Plan_ChangeThroughOutOfScopeIntermediatePropagatesToInScopeConsumer()
    {
        // Y is recorded in the manifest as embedding X's layout, but Y itself is outside the
        // current source set. Z is in scope and embeds Y's layout. Reaching Z requires both the
        // BFS walk (Z is two hops from the change) and a reverse graph built from every recorded
        // source rather than only the in-scope ones.
        var paths = new[] { "Game/X.uasset", "Game/Y.uasset", "Game/Z.uasset" };
        var manifest = ManifestWithLayoutEdges(
            paths, ("Game/Y.uasset", "Game/X.uasset"), ("Game/Z.uasset", "Game/Y.uasset"));
        var fingerprints = PlanInputsFixture.Fingerprints(paths);
        fingerprints["Game/X.uasset"] = "patched";

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/X.uasset", "Game/Z.uasset"),
            fingerprints: fingerprints));

        Assert.Equal(["Game/X.uasset", "Game/Z.uasset"], result.Plan!.WorkList.Order());
    }
}
