using System.Collections.Generic;
using System.Linq;

namespace UnrealAssetScout.Incremental;

// The whole incremental decision, as a pure function from plain data to an ExportPlan.
// Called by IncrementalRunner before any export work happens. It gates on the conditions that make
// an incremental run unsafe, finds directly stale sources, propagates that staleness to a fixpoint
// over reverse layout-provider edges (reverse import edges for the modes whose converters embed
// cross-package data), and splits S into a work list and a carry-forward set.
// It touches no disk, no provider and no CUE4Parse, which is the main defence against the failure
// mode that matters: a wrongly skipped package leaving a stale file nobody notices.
internal static class ExportPlanner
{
    internal static PlanResult Plan(PlanInputs inputs)
    {
        var all = inputs.Sources.Keys.Order().ToList();

        if (inputs.Rebuild || inputs.Manifest is null)
            return PlanResult.Ok(new ExportPlan(
                all, [], null, [inputs.Tool], PlanStatistics.ForFullRun(all.Count)));

        var manifest = inputs.Manifest;

        if (!string.Equals(manifest.Mode, inputs.Mode, System.StringComparison.Ordinal))
            return PlanResult.Failed(
                $"manifest was written for mode '{manifest.Mode}', this run is '{inputs.Mode}'; " +
                "pass --rebuild to replace it");

        if (!string.Equals(manifest.Game, inputs.Game, System.StringComparison.Ordinal))
            return PlanResult.Failed(
                $"manifest was written for game '{manifest.Game}', this run is '{inputs.Game}'; " +
                "pass --rebuild to replace it");

        var mounted = new HashSet<string>(inputs.Containers, System.StringComparer.OrdinalIgnoreCase);
        var missing = manifest.Containers.FirstOrDefault(container => !mounted.Contains(container));
        if (missing is not null)
            return PlanResult.Failed(
                $"container '{missing}' is recorded in the manifest but is not mounted; " +
                "check --paks and the AES key, or pass --rebuild");

        var toolVersions = ResolveToolVersions(manifest.Tool, inputs.Tool, inputs.AcceptToolVersion);
        if (toolVersions is null)
        {
            var recorded = manifest.Tool[^1];
            return PlanResult.Failed(
                $"manifest records uas {recorded.Uas} with CUE4Parse {recorded.Cue4Parse}; " +
                $"this run is uas {inputs.Tool.Uas} with CUE4Parse {inputs.Tool.Cue4Parse}. " +
                "Pass --rebuild for a guaranteed-exact dump, or --accept-tool-version to carry " +
                "existing outputs forward");
        }

        var changedTypes = ChangedUsmapNames(manifest, inputs.Usmap);
        var closure = new UsmapClosure(inputs.Usmap);
        var index = new ManifestIndex(manifest);

        var reasons = new Dictionary<StaleReason, int>();
        var stale = FindDirectlyStale(inputs, index, changedTypes, closure, reasons);
        var directlyStale = stale.Count;
        Propagate(index, stale, inputs.Mode, inputs.ResolvePackagePath);
        reasons[StaleReason.Propagated] = stale.Count - directlyStale;

        var workList = all.Where(stale.Contains).ToList();
        var carryForward = all.Where(path => !stale.Contains(path)).ToList();

        return PlanResult.Ok(new ExportPlan(
            workList, carryForward, manifest, toolVersions,
            Summarise(workList, carryForward, index, reasons)));
    }

    // Returns null when the gate should fire.
    private static IReadOnlyList<ToolVersionPair>? ResolveToolVersions(
        IReadOnlyList<ToolVersionPair> recorded, ToolVersionPair current, bool accept)
    {
        var known = recorded.Contains(current);
        if (!known && !accept)
            return null;

        // Most recently used last, so the tail is always the version that wrote this manifest.
        return [.. recorded.Where(pair => pair != current), current];
    }

    private static HashSet<string> FindDirectlyStale(
        PlanInputs inputs, ManifestIndex index, IReadOnlySet<string> changedTypes, UsmapClosure closure,
        Dictionary<StaleReason, int> reasons)
    {
        var stale = new HashSet<string>();

        foreach (var (path, candidate) in inputs.Sources)
        {
            if (!index.TryGetSource(path, out var entry))
            {
                stale.Add(path);
                reasons[StaleReason.NewSource] = reasons.GetValueOrDefault(StaleReason.NewSource) + 1;
                continue;
            }

            var reason = ClassifyStaleness(inputs, index, candidate, entry, changedTypes, closure);
            if (reason is StaleReason.None)
                continue;

            stale.Add(path);
            reasons[reason] = reasons.GetValueOrDefault(reason) + 1;
        }

        return stale;
    }

    // A source the previous manifest never had is an addition; everything else in the work list
    // is an update whose previous cost is known, except where that manifest predates cost
    // recording, which is counted rather than folded into the total as a zero.
    private static PlanStatistics Summarise(
        IReadOnlyList<string> workList, IReadOnlyList<string> carryForward, ManifestIndex index,
        IReadOnlyDictionary<StaleReason, int> reasons)
    {
        var added = 0;
        var updated = 0;
        var updateCost = 0d;
        var withoutCost = 0;

        foreach (var path in workList)
        {
            if (!index.TryGetSource(path, out var entry))
            {
                added++;
                continue;
            }

            updated++;
            if (entry.Ms > 0)
                updateCost += entry.Ms;
            else
                withoutCost++;
        }

        return new PlanStatistics(added, updated, carryForward.Count, updateCost, withoutCost, reasons);
    }

    // Returns the first rule that fires, so the plan summary can name why a source is stale.
    private static StaleReason ClassifyStaleness(
        PlanInputs inputs, ManifestIndex index, SourceCandidate candidate, ManifestSource entry,
        IReadOnlySet<string> changedTypes, UsmapClosure closure)
    {
        var recordedConstituents = entry.C.Select(index.PathOf).ToList();
        if (recordedConstituents.Count != candidate.Constituents.Count ||
            !recordedConstituents.OrderBy(path => path).SequenceEqual(candidate.Constituents.OrderBy(path => path)))
        {
            return StaleReason.ConstituentSetChanged;
        }

        if (recordedConstituents.Any(path => HasChanged(inputs, index, path)))
            return StaleReason.ConstituentContentChanged;

        if (entry.D.Select(index.PathOf).Any(dependency => HasDependencyChanged(inputs, index, dependency)))
            return StaleReason.DependencyChanged;

        if (entry.O.Select(index.OutputOf).Any(output => !inputs.OutputExists(output)))
            return StaleReason.OutputMissing;

        if (entry.E)
            return StaleReason.ExternalMedia;

        if (inputs.ScriptBytecode != inputs.Manifest!.ScriptBytecode &&
            entry.B is not BytecodeState.False)
        {
            return StaleReason.BytecodeFlagFlipped;
        }

        if (SkipPredicate(index, entry, inputs.Manifest.SkipTypes) !=
            SkipPredicate(index, entry, inputs.SkipTypes))
        {
            return StaleReason.SkipListChanged;
        }

        if (closure.IntersectsAny(index.TypeNamesOf(entry.T), changedTypes))
            return StaleReason.UsmapTypeChanged;

        if (index.TypeNamesOf(entry.U).Any(name =>
                inputs.Usmap.TypeFingerprints.ContainsKey(name) ||
                inputs.Usmap.EnumFingerprints.ContainsKey(name)))
        {
            return StaleReason.BlockedNameNowKnown;
        }

        return StaleReason.None;
    }

    // Mirrors JsonPackageProcessor.ShouldSkipJsonExport, evaluated from recorded names instead of
    // live objects; if that predicate changes, this must change with it.
    private static bool SkipPredicate(ManifestIndex index, ManifestSource entry, IReadOnlyList<string> skipTypes)
    {
        if (skipTypes.Count == 0)
            return false;

        var leaves = index.ClrNamesOf(entry.X);
        if (leaves.Count == 0)
            return false;

        var skipSet = new HashSet<string>(skipTypes, System.StringComparer.OrdinalIgnoreCase);
        return leaves.All(leaf => index.ClrChainOf(leaf).Any(skipSet.Contains));
    }

    // Present and different, or absent, both count as changed. Contrast HasDependencyChanged
    // below, where absent on both sides does not.
    private static bool HasChanged(PlanInputs inputs, ManifestIndex index, string path)
    {
        if (!inputs.Fingerprints.TryGetValue(path, out var current))
            return true;

        return !string.Equals(index.FingerprintOf(path), current, System.StringComparison.Ordinal);
    }

    // Absent on both sides means the dependency was unresolved before and still is, and does not
    // count as a change; present on exactly one side does.
    private static bool HasDependencyChanged(PlanInputs inputs, ManifestIndex index, string dependency)
    {
        var recorded = index.FingerprintOf(dependency);
        var current = CurrentDependencyFingerprint(inputs, dependency);

        if (recorded is null && current is null)
            return false;

        return !string.Equals(recorded, current, System.StringComparison.Ordinal);
    }

    // A package identity is a UE package name, which always starts with '/', or a "packageid:"
    // token, PackageDependencyReader's fallback for IoStore imports whose container path cannot be
    // turned back into a package name. IncrementalRunner.IsPackageIdentity must match this exactly.
    private static bool IsPackageIdentity(string dependency) =>
        dependency.StartsWith('/') || dependency.StartsWith("packageid:", System.StringComparison.Ordinal);

    private static string? CurrentDependencyFingerprint(PlanInputs inputs, string dependency)
    {
        var path = IsPackageIdentity(dependency) ? inputs.ResolvePackagePath(dependency) : dependency;

        return path is not null && inputs.Fingerprints.TryGetValue(path, out var hash) ? hash : null;
    }

    // Conversion output in these modes embeds data from packages the converters load on their own,
    // such as a skeleton for an animation or materials for a mesh, which layout providers do not
    // record, so staleness must keep travelling over every import edge there.
    private static readonly IReadOnlySet<string> ConverterEmbeddingModes =
        new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "models", "animations" };

    // The stale set doubles as the visited set, which is what terminates UE's mutual import
    // cycles. Edges come from every recorded source, not just the ones in S, so a stale package
    // outside the current scope still invalidates the in-scope packages that import it.
    // For most modes the edges are the recorded layout providers (`p`). The direct dependency
    // rule already covers one hop of any content change, and must stay that broad: a rendered
    // reference carries the target package's export index, and any content change there can
    // shift it. Beyond that hop a package renders indices only for its own imports, so only an
    // embedded layout can carry a change further, and that is what `p` records.
    private static void Propagate(
        ManifestIndex index, HashSet<string> stale, string mode, System.Func<string, string?> resolvePackagePath)
    {
        var followImports = ConverterEmbeddingModes.Contains(mode);

        // Layout providers record a package name without an extension, so they resolve to source
        // paths through a stem index instead of the provider's own path form.
        var sourceByStem = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        if (!followImports)
        {
            foreach (var (sourcePath, _) in index.Sources())
            {
                var extensionStart = sourcePath.LastIndexOf('.');
                sourceByStem[extensionStart >= 0 ? sourcePath[..extensionStart] : sourcePath] = sourcePath;
            }
        }

        // OrdinalIgnoreCase to match the provider, which this application always configures with
        // StringComparer.OrdinalIgnoreCase. A stricter comparer here would lose a reverse edge
        // rather than add one: a target resolved with different casing than the importer's
        // recorded path would silently fail to find its importers, under-invalidating.
        var importersOf = new Dictionary<string, List<string>>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var (sourcePath, entry) in index.Sources())
        {
            var edges = followImports ? entry.D : entry.P;
            foreach (var edge in edges.Select(index.PathOf))
            {
                string? target;
                if (IsPackageIdentity(edge))
                    target = resolvePackagePath(edge);
                else if (followImports)
                    target = edge;
                else
                    target = sourceByStem.GetValueOrDefault(edge);

                if (target is null)
                    continue;

                if (!importersOf.TryGetValue(target, out var importers))
                    importersOf[target] = importers = [];

                importers.Add(sourcePath);
            }
        }

        var queue = new Queue<string>(stale);
        while (queue.Count > 0)
        {
            if (!importersOf.TryGetValue(queue.Dequeue(), out var importers))
                continue;

            foreach (var importer in importers)
            {
                if (stale.Add(importer))
                    queue.Enqueue(importer);
            }
        }
    }

    private static IReadOnlySet<string> ChangedUsmapNames(ExportManifest manifest, UsmapSnapshot current)
    {
        // Must match UsmapClosure's visited-set comparer: names here can differ in case from what
        // UsmapClosure.Of walks, even after propagating CUE4Parse's own Types comparer.
        var changed = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        CollectChanges(
            manifest.Usmap.Types.ToDictionary(pair => manifest.UeTypes[pair.Key], pair => pair.Value),
            current.TypeFingerprints, changed);
        CollectChanges(
            manifest.Usmap.Enums.ToDictionary(pair => manifest.UeEnums[pair.Key], pair => pair.Value),
            current.EnumFingerprints, changed);

        return changed;
    }

    private static void CollectChanges(
        Dictionary<string, string> recorded, IReadOnlyDictionary<string, string> current, HashSet<string> changed)
    {
        foreach (var (name, fingerprint) in recorded)
        {
            if (!current.TryGetValue(name, out var now) || !string.Equals(now, fingerprint, System.StringComparison.Ordinal))
                changed.Add(name);
        }

        foreach (var name in current.Keys)
        {
            if (!recorded.ContainsKey(name))
                changed.Add(name);
        }
    }

    // String-level read access to an interned manifest, so the staleness rules never touch raw ids.
    private sealed class ManifestIndex
    {
        private readonly ExportManifest _manifest;
        private readonly Dictionary<string, int> _pathIds;
        private readonly Dictionary<string, int> _clrTypeIds;

        internal ManifestIndex(ExportManifest manifest)
        {
            _manifest = manifest;
            _pathIds = new Dictionary<string, int>(manifest.Paths.Count);
            for (var id = 0; id < manifest.Paths.Count; id++)
                _pathIds[manifest.Paths[id]] = id;

            _clrTypeIds = new Dictionary<string, int>(manifest.ClrTypes.Count);
            for (var id = 0; id < manifest.ClrTypes.Count; id++)
                _clrTypeIds[manifest.ClrTypes[id]] = id;
        }

        internal bool TryGetSource(string path, out ManifestSource entry)
        {
            if (_pathIds.TryGetValue(path, out var id) && _manifest.Sources.TryGetValue(id, out var found))
            {
                entry = found;
                return true;
            }

            entry = null!;
            return false;
        }

        internal string PathOf(int id) => _manifest.Paths[id];

        internal string OutputOf(int id) => _manifest.Outputs[id];

        internal string? FingerprintOf(string path) =>
            _pathIds.TryGetValue(path, out var id) && _manifest.Fingerprints.TryGetValue(id, out var hash)
                ? hash
                : null;

        internal IReadOnlyList<string> TypeNamesOf(int? setId) =>
            setId is { } id ? _manifest.TypeSets[id].Select(nameId => _manifest.UeTypes[nameId]).ToList() : [];

        internal IReadOnlyList<string> ClrNamesOf(int? setId) =>
            setId is { } id ? _manifest.ClrTypeSets[id].Select(nameId => _manifest.ClrTypes[nameId]).ToList() : [];

        // The [leaf] fallback keeps an unknown leaf matching only itself, which is what a bare
        // leaf name would do anyway.
        internal IReadOnlyList<string> ClrChainOf(string leaf) =>
            _clrTypeIds.TryGetValue(leaf, out var id) && _manifest.ClrTypeChains.TryGetValue(id, out var chain)
                ? chain.Select(nameId => _manifest.ClrTypes[nameId]).ToList()
                : [leaf];

        internal IEnumerable<(string SourcePath, ManifestSource Entry)> Sources() =>
            _manifest.Sources.Select(pair => (_manifest.Paths[pair.Key], pair.Value));
    }
}
