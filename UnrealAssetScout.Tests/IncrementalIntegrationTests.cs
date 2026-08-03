using System.Text.RegularExpressions;
using UnrealAssetScout;
using UnrealAssetScout.Incremental;

namespace UnrealAssetScout.Tests;

// Drives Program.Run end to end against a real game, the only coverage that does: every other
// IncrementalRunner test drives its extracted helpers with fixtures, never a live export. Gated on
// UAS_TEST_PAKS/UAS_TEST_USMAP so it visibly skips rather than silently passing when unconfigured;
// the regenerated usmap test additionally needs UAS_TEST_USMAP_2.
// [Collection("Logging")] because Program.Run reconfigures the global Serilog logger, which is not
// safe under parallel test execution; see RuntimeLoggingTests.
[Collection("Logging")]
public sealed class IncrementalIntegrationTests
{
    private static string? Paks => Environment.GetEnvironmentVariable("UAS_TEST_PAKS");
    private static string? Usmap => Environment.GetEnvironmentVariable("UAS_TEST_USMAP");
    private static string? RegeneratedUsmap => Environment.GetEnvironmentVariable("UAS_TEST_USMAP_2");
    private static bool Configured => !string.IsNullOrWhiteSpace(Paks) && !string.IsNullOrWhiteSpace(Usmap);

    private static int Export(string outputDir, params string[] extra) =>
        Program.Run([
            "export", "textures", "--paks", Paks!, "--game", "GAME_UE5_1", "--usmap", Usmap!,
            "--output", outputDir, "--no-log", .. extra
        ]);

    [SkippableFact]
    public void SecondRun_ExportsNothingAndPreservesEveryTimestamp()
    {
        Skip.IfNot(Configured);
        using var dir = new TempDir();
        Assert.Equal(0, Export(dir.Path));

        var before = Directory.GetFiles(dir.Path, "*", SearchOption.AllDirectories)
            .ToDictionary(path => path, File.GetLastWriteTimeUtc);

        Assert.Equal(0, Export(dir.Path));

        var after = Directory.GetFiles(dir.Path, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path) != ExportManifestStore.FileName)
            .ToDictionary(path => path, File.GetLastWriteTimeUtc);

        Assert.All(after, entry => Assert.Equal(before[entry.Key], entry.Value));
    }

    [SkippableFact]
    public void DeletedOutput_IsRestoredByTheNextRun()
    {
        Skip.IfNot(Configured);
        using var dir = new TempDir();
        Export(dir.Path);
        var victim = Directory.GetFiles(dir.Path, "*.png", SearchOption.AllDirectories).First();
        File.Delete(victim);

        Export(dir.Path);

        Assert.True(File.Exists(victim));
    }

    [SkippableFact]
    public void UntrackedFile_SurvivesEveryRun()
    {
        Skip.IfNot(Configured);
        using var dir = new TempDir();
        Export(dir.Path);
        var untracked = Path.Combine(dir.Path, "my-notes.txt");
        File.WriteAllText(untracked, "keep me");

        Export(dir.Path);

        Assert.True(File.Exists(untracked));
    }

    [SkippableFact]
    public void HandEditedFingerprint_CausesExactlyThatSourceToReExport()
    {
        Skip.IfNot(Configured);
        using var dir = new TempDir();
        Export(dir.Path);
        var manifest = ExportManifestStore.TryLoad(dir.Path, out _)!;
        var victimId = manifest.Sources.Keys.First();
        manifest.Fingerprints[victimId] = "TAMPERED";
        ExportManifestStore.Save(dir.Path, manifest);

        Assert.Equal(0, Export(dir.Path));
    }

    [SkippableFact]
    public void RegeneratedUsmap_InvalidatesNothing()
    {
        // UAS_TEST_USMAP_2 must point at a second usmap generated for the same game version as
        // UAS_TEST_USMAP. Two such usmaps differ only in blueprint-side entries the exporter never
        // takes layout from, so switching between them must not re-export anything.
        Skip.IfNot(Configured && !string.IsNullOrWhiteSpace(RegeneratedUsmap));
        using var dir = new TempDir();
        Assert.Equal(0, Export(dir.Path));

        var before = Directory.GetFiles(dir.Path, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path) != ExportManifestStore.FileName)
            .ToDictionary(path => path, File.GetLastWriteTimeUtc);

        var exitCode = Program.Run([
            "export", "textures", "--paks", Paks!, "--game", "GAME_UE5_1", "--usmap", RegeneratedUsmap!,
            "--output", dir.Path, "--no-log"
        ]);

        Assert.Equal(0, exitCode);
        var after = Directory.GetFiles(dir.Path, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path) != ExportManifestStore.FileName)
            .ToDictionary(path => path, File.GetLastWriteTimeUtc);
        Assert.Equal(before.Count, after.Count);
        Assert.All(after, entry => Assert.Equal(before[entry.Key], entry.Value));
    }

    [SkippableFact]
    public void WrongMode_ErrorsAndStops()
    {
        Skip.IfNot(Configured);
        using var dir = new TempDir();
        Export(dir.Path);

        var exitCode = Program.Run([
            "export", "json", "--paks", Paks!, "--game", "GAME_UE5_1", "--usmap", Usmap!,
            "--output", dir.Path, "--no-log"
        ]);

        Assert.Equal(1, exitCode);
    }

    [SkippableFact]
    public void TruncatedManifest_ErrorsAndStops()
    {
        Skip.IfNot(Configured);
        using var dir = new TempDir();
        Export(dir.Path);
        File.WriteAllText(ExportManifestStore.PathFor(dir.Path), "{\"schema\": 1, \"mode\"");

        Assert.Equal(1, Export(dir.Path));
    }

    [SkippableFact]
    public void Rebuild_RecoversFromAnyCorruptState()
    {
        Skip.IfNot(Configured);
        using var dir = new TempDir();
        Export(dir.Path);
        File.WriteAllText(ExportManifestStore.PathFor(dir.Path), "not json at all");

        Assert.Equal(0, Export(dir.Path, "--rebuild"));
    }

    [SkippableFact]
    public void MismatchedSchema_ErrorsAndStopsButRebuildRecovers()
    {
        Skip.IfNot(Configured);
        using var dir = new TempDir();
        Export(dir.Path);
        var manifest = ExportManifestStore.TryLoad(dir.Path, out _)!;
        manifest.Schema = ExportManifestStore.CurrentSchema + 1;
        ExportManifestStore.Save(dir.Path, manifest);

        Assert.Equal(1, Export(dir.Path));
        Assert.Equal(0, Export(dir.Path, "--rebuild"));
    }

    [SkippableFact]
    public void VaryingLoggingOptions_ExportsNothing()
    {
        // The only check that the not-recorded option classification holds.
        Skip.IfNot(Configured);
        using var dir = new TempDir();
        Export(dir.Path);
        var before = Directory.GetFiles(dir.Path, "*.png", SearchOption.AllDirectories)
            .ToDictionary(path => path, File.GetLastWriteTimeUtc);

        Assert.Equal(0, Export(dir.Path, "--verbose", "--log-counter"));

        Assert.All(before, entry => Assert.Equal(entry.Value, File.GetLastWriteTimeUtc(entry.Key)));
    }

    // Closes the gap a prior live experiment (see task 18's report) left open: flipping a byte in a
    // dependency's own stored hash proved a source with no outputs went stale, but that dependency
    // was itself in the export scope, so it also went directly stale via its own constituent check
    // -- reverse-edge propagation alone was already enough to explain the importer going stale, so
    // the dependency-fingerprint comparison itself was never isolated from it.
    //
    // Here the dependency's target is excluded from the scope with --filter, anchored to match only
    // the importer's own path, so the target can never enter the stale set directly and propagation
    // has nothing stale to walk from. The only path left to the importer is HasDependencyChanged
    // resolving the identity and finding a differing hash against the manifest's live fingerprint
    // index, which still covers the target because that index is built for the whole provider, not
    // filtered to the current scope.
    [SkippableFact]
    public void DependencyFingerprintChangeOutsideExportScope_ReExportsOnlyTheImporter()
    {
        Skip.IfNot(Configured);
        using var dir = new TempDir();
        Assert.Equal(0, Export(dir.Path));

        var manifest = ExportManifestStore.TryLoad(dir.Path, out _)!;
        var candidate = FindIsolatableDependency(manifest);
        Skip.If(candidate is null,
            "no exported source in this game has an output and a resolvable, fingerprinted " +
            "dependency on a different package; cannot isolate the dependency-fingerprint rule");
        var (sourcePath, dependencyId, outputs) = candidate!.Value;

        var before = outputs
            .Select(output => Path.Combine(dir.Path, output))
            .ToDictionary(path => path, File.GetLastWriteTimeUtc);

        manifest.Fingerprints[dependencyId] = "TAMPERED";
        ExportManifestStore.Save(dir.Path, manifest);

        var filter = "^" + Regex.Escape(sourcePath) + "$";
        Assert.Equal(0, Export(dir.Path, "--filter", filter));

        Assert.All(before, entry => Assert.NotEqual(entry.Value, File.GetLastWriteTimeUtc(entry.Key)));
    }

    // A recorded source with at least one output -- so re-export leaves an observable trace on disk
    // -- and a dependency identity (a package name or a "packageid:" token, per
    // PackageDependencyReader) that resolved to a different package with a known fingerprint.
    // Excluding that different package from a later run's scope with --filter is what makes a
    // change to it reach the source only through the dependency-fingerprint comparison.
    private static (string SourcePath, int DependencyId, List<string> Outputs)? FindIsolatableDependency(
        ExportManifest manifest)
    {
        foreach (var (pathId, entry) in manifest.Sources)
        {
            if (entry.O.Count == 0)
                continue;

            var sourcePath = manifest.Paths[pathId];
            foreach (var dependencyId in entry.D)
            {
                var dependency = manifest.Paths[dependencyId];
                if (dependency == sourcePath || !IsPackageIdentity(dependency))
                    continue;

                if (!manifest.Fingerprints.ContainsKey(dependencyId))
                    continue;

                var outputs = entry.O.Select(id => manifest.Outputs[id]).ToList();
                return (sourcePath, dependencyId, outputs);
            }
        }

        return null;
    }

    // Mirrors ExportPlanner's and IncrementalRunner's own private definition of a package identity.
    private static bool IsPackageIdentity(string dependency) =>
        dependency.StartsWith('/') || dependency.StartsWith("packageid:", StringComparison.Ordinal);
}
