using UnrealAssetScout.Incremental;

namespace UnrealAssetScout.Tests;

// Covers IncrementalRunner.DeduplicateByPath in isolation, with plain SourceRecord fixtures
// standing in for what ExportProcessor's per-container double-open of a shadowed path would
// actually produce. See the call site's own comment for why keeping the first is correct: it
// matches the order the provider's own resolution (and therefore SourceFingerprintIndex) uses.
public sealed class IncrementalRunnerRecordDeduplicationTests
{
    private static SourceRecord Record(string path, string status) =>
        new() { Path = path, Bytecode = BytecodeState.False, Status = status };

    [Fact]
    public void DeduplicateByPath_NoDuplicates_KeepsEveryRecord()
    {
        var records = IncrementalRunner.DeduplicateByPath(
            [Record("Game/A.uasset", "ok"), Record("Game/B.uasset", "ok")], StringComparer.OrdinalIgnoreCase);

        Assert.Equal(["Game/A.uasset", "Game/B.uasset"], records.Select(record => record.Path));
    }

    [Fact]
    public void DeduplicateByPath_ShadowedPath_KeepsOnlyTheFirstRecord()
    {
        // The first record stands in for the patch container's pass: ExportProcessor iterates
        // provider.Files.Values, which yields the highest-read-order (patch) entry before the
        // base entry for a shadowed path, matching the provider's own indexer resolution.
        var patch = Record("Game/A.uasset", "ok");
        var stale = new SourceRecord { Path = "Game/A.uasset", Bytecode = BytecodeState.False, Status = "failed" };

        var records = IncrementalRunner.DeduplicateByPath([patch, stale], StringComparer.OrdinalIgnoreCase);

        Assert.Same(patch, Assert.Single(records));
    }

    [Fact]
    public void DeduplicateByPath_UsesTheSuppliedComparer()
    {
        var first = Record("Game/A.uasset", "ok");
        var differentCase = Record("GAME/A.UASSET", "failed");

        var caseInsensitive = IncrementalRunner.DeduplicateByPath([first, differentCase], StringComparer.OrdinalIgnoreCase);
        Assert.Same(first, Assert.Single(caseInsensitive));

        var caseSensitive = IncrementalRunner.DeduplicateByPath([first, differentCase], StringComparer.Ordinal);
        Assert.Equal(2, caseSensitive.Count());
    }
}
