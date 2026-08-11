using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CUE4Parse.FileProvider.Vfs;
using CUE4Parse.UE4.IO.Objects;
using UnrealAssetScout.Config;
using UnrealAssetScout.Export;
using UnrealAssetScout.Logging;
using UnrealAssetScout.Statistics;
using UnrealAssetScout.Utils;

namespace UnrealAssetScout.Incremental;

// Wires PLAN, EXECUTE and COMMIT together for one export run.
// Called by Program.Run in place of a bare ExportProcessor.ProcessFiles call whenever a mode is
// selected. Incremental is automatic when a manifest is present; a missing manifest is the only
// case that implicitly runs full, and everything else that is wrong stops and names the problem.
// The manifest is written last, so a run that dies partway leaves the previous good state intact.
// Orphans are always found by comparing the previous manifest against the one COMMIT just built,
// never against the plan: only once every re-exported source has actually run is it known what it
// produced, which is what catches a source that used to emit more outputs than it does now.
internal static class IncrementalRunner
{
    internal static (int ExitCode, RunStats? Stats) Run(
        AbstractVfsFileProvider provider,
        Options options,
        LogLevelCounterSink? compactCounterSink,
        IReadOnlySet<string>? typeFilteredPaths)
    {
        var outputDir = options.OutputDirectory!;
        var mode = options.Mode!.Value;
        var isJsonMode = mode == ExportMode.Json;
        var effectiveScriptBytecode = isJsonMode && options.ScriptBytecode;

        var planWatch = Stopwatch.StartNew();
        var stepWatch = Stopwatch.StartNew();
        var previous = ExportManifestStore.TryLoad(outputDir, out var loadError);
        if (loadError is not null && !options.Rebuild)
        {
            AppLog.Error("{Message}", loadError);
            return (1, null);
        }

        AnnouncePlanStart(previous, options, outputDir);
        var manifestMillis = stepWatch.ElapsedMilliseconds;
        stepWatch.Restart();

        var usmap = UsmapFingerprints.From(provider.MappingsForGame);
        var fingerprints = SourceFingerprintIndex.Build(provider);
        var fingerprintMillis = stepWatch.ElapsedMilliseconds;
        stepWatch.Restart();

        var sources = SourceSetBuilder.Build(
            SourceFingerprintIndex.ResolvedFiles(provider).Select(file => file.Path),
            mode, options.Filter, typeFilteredPaths);
        var containers = provider.MountedVfs.Select(vfs => Path.GetFileName(vfs.Path)).Order().ToList();
        var sourceSetMillis = stepWatch.ElapsedMilliseconds;
        stepWatch.Restart();

        var result = ExportPlanner.Plan(new PlanInputs(
            Manifest: previous,
            Mode: mode.ToString().ToLowerInvariant(),
            Game: options.Game!.Value.ToString(),
            Tool: new ToolVersionPair(AppVersion.UasVersionText, AppVersion.Cue4ParseVersionText),
            Containers: containers,
            SkipTypes: options.JsonSkipTypeNames,
            ScriptBytecode: effectiveScriptBytecode,
            Sources: sources,
            Fingerprints: fingerprints.ByPath,
            Usmap: usmap,
            OutputExists: relative => File.Exists(Path.Combine(outputDir, relative)),
            ResolvePackagePath: identity => ResolvePackagePath(provider, identity),
            Rebuild: options.Rebuild,
            AcceptToolVersion: options.AcceptToolVersion));

        if (result.Error is not null)
        {
            AppLog.Error("{Message}", result.Error.Replace("manifest ", $"manifest at {ExportManifestStore.PathFor(outputDir)} "));
            return (1, null);
        }

        var plan = result.Plan!;
        if (options.Verbose)
        {
            RuntimeLogging.LogSummary(
                "  plan steps: manifest {Manifest:N1}s, fingerprints {Fingerprints:N1}s ({Entries:N0} entries), " +
                "sources {Sources:N1}s ({InScope:N0} in scope), rules {Rules:N1}s",
                manifestMillis / 1000d, fingerprintMillis / 1000d, fingerprints.ByPath.Count,
                sourceSetMillis / 1000d, sources.Count, stepWatch.ElapsedMilliseconds / 1000d);

            var reasons = plan.Statistics.DescribeReasons();
            if (reasons.Length > 0)
                RuntimeLogging.LogSummary("  stale by: {Reasons}", reasons);
        }

        AnnouncePlanResult(plan, ProjectOrphans(previous, plan), planWatch.Elapsed);

        if (plan.HasMixedToolVersions)
        {
            AppLog.Warning(
                "dump was produced by {Count} tool versions; output is not guaranteed to match a full rebuild",
                plan.ToolVersions.Count);
        }

        if (options.DryRun)
        {
            RuntimeLogging.LogSummary("Dry run: nothing was written.");
            return (0, null);
        }

        var builder = new ManifestBuilder(
            mode.ToString().ToLowerInvariant(), options.Game.Value.ToString(), plan.ToolVersions,
            options.JsonSkipTypeNames, effectiveScriptBytecode, containers);

        var recorder = new SourceRecorder(outputDir, usmap, effectiveScriptBytecode, isJsonMode);
        var stats = ExportProcessor.ProcessFiles(
            provider, mode, outputDir, options.Filter, options.Verbose, options.MarkUsmap,
            compactCounterSink, typeFilteredPaths, options.LogCounter, options.JsonSkipTypeNames,
            incrementalWorkList: plan.Baseline is null ? null : new HashSet<string>(plan.WorkList),
            recorder: recorder,
            constituentsOf: path => sources.TryGetValue(path, out var candidate) ? candidate.Constituents : null);

        // ExportProcessor iterates provider.Files.Values, which yields a shadowed path once per
        // mounting container, so a source can be opened and closed twice for the same path. Keep
        // only the first: FileProviderDictionary enumerates highest read order first and its own
        // indexer resolves the same way, which is also what SourceFingerprintIndex resolved the
        // fingerprint from, so keeping the first is what keeps recorded metadata and the recorded
        // fingerprint describing the same underlying file.
        var records = DeduplicateByPath(recorder.Records, provider.PathComparer).ToList();

        foreach (var record in records)
            builder.AddRecorded(record);

        var dependencyIdentities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            foreach (var dependency in record.Dependencies)
            {
                if (IsPackageIdentity(dependency))
                    dependencyIdentities.Add(dependency);
            }
        }

        if (plan.Baseline is { } baseline)
        {
            var baselinePathIds = BuildPathIndex(baseline);
            foreach (var path in plan.CarryForward)
            {
                if (!TryFindEntry(baseline, baselinePathIds, path, out var entry))
                    continue;

                builder.CarryForward(path, baseline, entry);
                foreach (var dependencyId in entry.D)
                {
                    var dependency = baseline.Paths[dependencyId];
                    if (IsPackageIdentity(dependency))
                        dependencyIdentities.Add(dependency);
                }
            }
        }

        foreach (var (path, hash) in fingerprints.ByPath)
            builder.SetFingerprint(path, hash);

        foreach (var identity in dependencyIdentities)
        {
            var resolvedPath = ResolvePackagePath(provider, identity);
            if (resolvedPath is not null && fingerprints.ByPath.TryGetValue(resolvedPath, out var hash))
                builder.SetFingerprint(identity, hash);
        }

        builder.SetUsmap(builder.InternUsmap(usmap));
        var manifest = builder.Build();

        var commitWatch = Stopwatch.StartNew();
        var orphans = OrphanCleanup.FindOrphans(previous, manifest);
        var deleted = OrphanCleanup.Delete(outputDir, orphans);

        ExportManifestStore.Save(outputDir, manifest);
        RuntimeLogging.LogSummary(
            "Commit: deleted {Deleted:N0} orphaned output(s), manifest written in {Seconds:N1}s",
            deleted, commitWatch.Elapsed.TotalSeconds);
        return (0, stats);
    }

    private static void AnnouncePlanStart(ExportManifest? previous, Options options, string outputDir)
    {
        if (options.Rebuild)
        {
            RuntimeLogging.LogSummary("Planning: rebuild requested, every source will be exported ...");
            return;
        }

        if (previous is null)
        {
            RuntimeLogging.LogSummary("Planning: no previous manifest, every source will be exported ...");
            return;
        }

        RuntimeLogging.LogSummary(
            "Planning against the manifest written {Written:yyyy-MM-dd HH:mm} with {Sources:N0} sources ...",
            File.GetLastWriteTime(ExportManifestStore.PathFor(outputDir)), previous.Sources.Count);
    }

    private static void AnnouncePlanResult(ExportPlan plan, int projectedOrphans, TimeSpan elapsed)
    {
        var statistics = plan.Statistics;
        RuntimeLogging.LogSummary(
            "Plan: {Added:N0} to add, {Updated:N0} to update, up to {Deleted:N0} to delete, " +
            "{Unchanged:N0} unchanged (planned in {Seconds:N1}s)",
            statistics.Added, statistics.Updated, projectedOrphans, statistics.Unchanged,
            elapsed.TotalSeconds);

        if (statistics.Updated == 0)
            return;

        // The cost is what the previous run measured for exactly these sources, so it is a record
        // rather than a forecast; additions have never been exported and so have none.
        var unknown = statistics.UpdatesWithoutRecordedCost > 0
            ? $", {statistics.UpdatesWithoutRecordedCost:N0} of them without a recorded time"
            : string.Empty;
        RuntimeLogging.LogSummary(
            "Plan: the previous run spent {Cost} on the sources being updated{Unknown}",
            Formatting.FormatElapsed(TimeSpan.FromMilliseconds(statistics.UpdateCostMilliseconds)), unknown);
    }

    // Internal, not private, so it can be driven directly by plain SourceRecord fixtures with no
    // live provider; see IncrementalRunnerRecordDeduplicationTests. The order of `records` matters:
    // whichever comes first for a given path wins, which is only correct because the caller feeds
    // it entries in the provider's own resolution order.
    internal static IEnumerable<SourceRecord> DeduplicateByPath(
        IEnumerable<SourceRecord> records, IEqualityComparer<string> pathComparer)
    {
        var seen = new HashSet<string>(pathComparer);
        foreach (var record in records)
        {
            if (seen.Add(record.Path))
                yield return record;
        }
    }

    private static string? ResolvePackagePath(AbstractVfsFileProvider provider, string identity) =>
        ResolveIdentity(
            identity,
            id => provider.FilesById.TryGetValue(new FPackageId(id), out var idFile) ? idFile.Path : null,
            name => provider.TryGetGameFile(name, out var file) ? file.Path : null);

    // A malformed "packageid:" token (fails ulong.TryParse) returns null without ever calling
    // resolvePackageId -- the same "does not resolve" outcome ExportPlanner already expects from
    // an identity nothing currently mounts.
    internal static string? ResolveIdentity(
        string identity, Func<ulong, string?> resolvePackageId, Func<string, string?> resolveGameFilePath)
    {
        if (identity.StartsWith("packageid:", StringComparison.Ordinal))
        {
            var idText = identity["packageid:".Length..];
            return ulong.TryParse(idText, out var id) ? resolvePackageId(id) : null;
        }

        return resolveGameFilePath(identity);
    }

    // Must match ExportPlanner's own private IsPackageIdentity exactly: only identities, never
    // Wwise media container paths, get a fingerprint entry keyed by their own identity string.
    private static bool IsPackageIdentity(string dependency) =>
        dependency.StartsWith('/') || dependency.StartsWith("packageid:", StringComparison.Ordinal);

    // Built once per baseline manifest so every carried-forward source resolves its entry in O(1);
    // a per-source list scan would make a no-op run scale quadratically with dump size.
    private static Dictionary<string, int> BuildPathIndex(ExportManifest manifest)
    {
        var index = new Dictionary<string, int>(manifest.Paths.Count);
        for (var id = 0; id < manifest.Paths.Count; id++)
            index[manifest.Paths[id]] = id;

        return index;
    }

    private static bool TryFindEntry(
        ExportManifest manifest, IReadOnlyDictionary<string, int> pathIds, string path, out ManifestSource entry)
    {
        if (pathIds.TryGetValue(path, out var id) && manifest.Sources.TryGetValue(id, out var found))
        {
            entry = found;
            return true;
        }

        entry = null!;
        return false;
    }

    // Internal, not private, so it can be driven directly by ExportManifest/ExportPlan fixtures
    // with no live provider; see IncrementalRunnerOrphanProjectionTests.
    internal static int ProjectOrphans(ExportManifest? previous, ExportPlan plan)
    {
        if (previous is null)
            return 0;

        var pathIds = BuildPathIndex(previous);
        var surviving = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in plan.CarryForward)
        {
            if (!TryFindEntry(previous, pathIds, path, out var entry))
                continue;

            foreach (var id in entry.O)
                surviving.Add(previous.Outputs[id]);
        }

        return previous.Outputs.Count(output => !surviving.Contains(output));
    }
}
