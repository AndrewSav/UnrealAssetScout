using UnrealAssetScout.Incremental;

namespace UnrealAssetScout.Tests;

public sealed class OrphanCleanupTests
{
    private static ExportManifest ManifestClaiming(params string[] outputs)
    {
        var manifest = new ExportManifest { Outputs = [.. outputs] };
        manifest.Sources[0] = new ManifestSource
        {
            O = [.. Enumerable.Range(0, outputs.Length)], B = BytecodeState.False, S = SourceStatus.Ok
        };
        return manifest;
    }

    [Fact]
    public void FindOrphans_NoPreviousManifest_FindsNothing()
    {
        Assert.Empty(OrphanCleanup.FindOrphans(null, ManifestClaiming("a.json")));
    }

    [Fact]
    public void FindOrphans_OutputNoLongerClaimed_IsAnOrphan()
    {
        var orphans = OrphanCleanup.FindOrphans(
            ManifestClaiming("a.json", "b.json"), ManifestClaiming("a.json"));

        Assert.Equal(["b.json"], orphans);
    }

    [Fact]
    public void FindOrphans_SourceNowEmitsFewerFiles_FindsTheDroppedOne()
    {
        var orphans = OrphanCleanup.FindOrphans(
            ManifestClaiming("a_0.png", "a_1.png", "a_2.png"), ManifestClaiming("a_0.png", "a_1.png"));

        Assert.Equal(["a_2.png"], orphans);
    }

    [Fact]
    public void FindOrphans_OutputClaimedByADifferentSource_IsNotAnOrphan()
    {
        var previous = ManifestClaiming("shared.json");
        var current = new ExportManifest { Outputs = ["shared.json"] };
        current.Sources[42] = new ManifestSource { O = [0], B = BytecodeState.False, S = SourceStatus.Ok };

        Assert.Empty(OrphanCleanup.FindOrphans(previous, current));
    }

    [Fact]
    public void FindOrphans_OutputListedButUnclaimed_IsAnOrphan()
    {
        // An entry left in the outputs table that no source's `o` references.
        var current = new ExportManifest { Outputs = ["a.json", "stale.json"] };
        current.Sources[0] = new ManifestSource { O = [0], B = BytecodeState.False, S = SourceStatus.Ok };

        Assert.Equal(["stale.json"], OrphanCleanup.FindOrphans(ManifestClaiming("a.json", "stale.json"), current));
    }

    [Fact]
    public void Delete_RemovesOrphansAndPrunesDirectoriesTheyEmptied()
    {
        using var dir = new TempDir();
        dir.File(Path.Combine("Deep", "Nested", "gone.json"));

        var deleted = OrphanCleanup.Delete(dir.Path, [Path.Combine("Deep", "Nested", "gone.json")]);

        Assert.Equal(1, deleted);
        Assert.False(Directory.Exists(Path.Combine(dir.Path, "Deep")));
    }

    [Fact]
    public void Delete_LeavesDirectoriesThatStillHoldFiles()
    {
        using var dir = new TempDir();
        dir.File(Path.Combine("Deep", "gone.json"));
        dir.File(Path.Combine("Deep", "kept.txt"));

        OrphanCleanup.Delete(dir.Path, [Path.Combine("Deep", "gone.json")]);

        Assert.True(File.Exists(Path.Combine(dir.Path, "Deep", "kept.txt")));
    }

    [Fact]
    public void Delete_NeverRemovesTheOutputRoot()
    {
        using var dir = new TempDir();
        dir.File("only.json");

        OrphanCleanup.Delete(dir.Path, ["only.json"]);

        Assert.True(Directory.Exists(dir.Path));
    }

    [Fact]
    public void Delete_MissingOrphanIsNotAnError()
    {
        using var dir = new TempDir();

        Assert.Equal(0, OrphanCleanup.Delete(dir.Path, ["never-existed.json"]));
    }

    [Fact]
    public void Delete_LeavesUntrackedFilesAlone()
    {
        using var dir = new TempDir();
        dir.File("tracked.json");
        dir.File("notes-the-user-put-here.txt");

        OrphanCleanup.Delete(dir.Path, ["tracked.json"]);

        Assert.True(File.Exists(Path.Combine(dir.Path, "notes-the-user-put-here.txt")));
    }

    [Fact]
    public void Delete_RefusesAnOrphanWhoseRelativePathEscapesTheOutputRoot()
    {
        // Root is dir.Path/Export. The orphan's relative path escapes it with a leading "..",
        // landing on dir.Path/ExportOld/stuff/file.json, a sibling whose directory name merely
        // extends the root's name. Both locations live inside the TempDir, so nothing outside it
        // is ever at risk. The escaping file must survive untouched, and nothing may be counted
        // as deleted.
        using var dir = new TempDir();
        var outputDir = Path.Combine(dir.Path, "Export");
        Directory.CreateDirectory(outputDir);
        var escapedFile = dir.File(Path.Combine("ExportOld", "stuff", "file.json"));

        var orphan = Path.Combine("..", "ExportOld", "stuff", "file.json");
        var deleted = OrphanCleanup.Delete(outputDir, [orphan]);

        Assert.Equal(0, deleted);
        Assert.True(File.Exists(escapedFile));
        Assert.True(Directory.Exists(Path.Combine(dir.Path, "ExportOld", "stuff")));
    }

    [Fact]
    public void Delete_RefusesAnOrphanThatIsAnAbsolutePath()
    {
        // The orphan entry is itself a rooted path pointing at a file elsewhere inside the same
        // TempDir, rather than a path relative to outputDir. Path.Combine discards outputDir when
        // its second argument is already rooted, so without a containment check this would delete
        // exactly that path regardless of outputDir.
        using var dir = new TempDir();
        var outputDir = Path.Combine(dir.Path, "Export");
        Directory.CreateDirectory(outputDir);
        var elsewhereFile = dir.File(Path.Combine("Elsewhere", "file.json"));

        var deleted = OrphanCleanup.Delete(outputDir, [elsewhereFile]);

        Assert.Equal(0, deleted);
        Assert.True(File.Exists(elsewhereFile));
    }

    [Fact]
    public void Delete_DoesNotPruneADirectoryThatIsAReparsePoint()
    {
        // "Linked" is a real directory symlink under the output root, pointing at another
        // directory inside the same TempDir. Deleting the one file it holds empties its target,
        // but the link itself must survive: it is not an ordinary empty directory.
        using var dir = new TempDir();
        var outputDir = Path.Combine(dir.Path, "Output");
        Directory.CreateDirectory(outputDir);

        var linkTarget = Path.Combine(dir.Path, "LinkTarget");
        Directory.CreateDirectory(linkTarget);
        File.WriteAllText(Path.Combine(linkTarget, "gone.json"), "x");

        var linkPath = Path.Combine(outputDir, "Linked");
        Directory.CreateSymbolicLink(linkPath, linkTarget);

        var deleted = OrphanCleanup.Delete(outputDir, [Path.Combine("Linked", "gone.json")]);

        Assert.Equal(1, deleted);
        Assert.True(Directory.Exists(linkPath));
    }
}
