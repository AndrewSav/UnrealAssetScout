using UnrealAssetScout.Export;
using UnrealAssetScout.Incremental;

namespace UnrealAssetScout.Tests;

public sealed class SourceRecorderTests
{
    private static SourceRecorder NewRecorder(bool scriptBytecode = false, bool isJsonMode = true) =>
        new(outputDir: Path.Combine("C:", "out"), usmap: UsmapSnapshot.Empty,
            scriptBytecode: scriptBytecode, isJsonMode: isJsonMode);

    [Fact]
    public void EndSource_ProducesOneRecordPerSource()
    {
        var recorder = NewRecorder();

        recorder.BeginSource("Game/A.uasset", ["Game/A.uasset"]);
        recorder.EndSource(SourceStatus.Ok);
        recorder.BeginSource("Game/B.uasset", ["Game/B.uasset"]);
        recorder.EndSource(SourceStatus.Ok);

        Assert.Equal(["Game/A.uasset", "Game/B.uasset"], recorder.Records.Select(record => record.Path));
    }

    [Fact]
    public void EndSource_RecordsHowLongTheSourceTook()
    {
        var recorder = NewRecorder();

        recorder.BeginSource("Game/Slow.uasset", ["Game/Slow.uasset"]);
        Thread.Sleep(30);
        recorder.EndSource(SourceStatus.Ok);

        // Generous lower bound: the sleep is the floor, and a loaded machine only ever overshoots.
        Assert.True(recorder.Records.Single().Milliseconds >= 15,
            $"expected at least 15 ms, got {recorder.Records.Single().Milliseconds}");
    }

    [Fact]
    public void EndSource_TimesEachSourceSeparately()
    {
        // A single shared stopwatch, or one never restarted, would make the second source inherit
        // the first one's elapsed time and report at least as much.
        var recorder = NewRecorder();

        recorder.BeginSource("Game/Slow.uasset", ["Game/Slow.uasset"]);
        Thread.Sleep(30);
        recorder.EndSource(SourceStatus.Ok);

        recorder.BeginSource("Game/Fast.uasset", ["Game/Fast.uasset"]);
        recorder.EndSource(SourceStatus.Ok);

        var slow = recorder.Records[0].Milliseconds;
        var fast = recorder.Records[1].Milliseconds;
        Assert.True(fast < slow, $"expected the second source to be faster; slow {slow}, fast {fast}");
    }

    [Fact]
    public void AddArtifacts_StoresOutputsRelativeToTheOutputDirectory()
    {
        var recorder = NewRecorder();
        recorder.BeginSource("Game/A.uasset", ["Game/A.uasset"]);

        recorder.AddArtifacts([new ExportedArtifact("Game/A", Path.Combine("C:", "out", "Game", "A.json"))]);
        recorder.EndSource(SourceStatus.Ok);

        Assert.Equal([Path.Combine("Game", "A.json")], Assert.Single(recorder.Records).Outputs);
    }

    [Fact]
    public void AddArtifacts_DeduplicatesRepeatedOutputPaths()
    {
        var recorder = NewRecorder();
        recorder.BeginSource("Game/A.uasset", ["Game/A.uasset"]);
        var artifact = new ExportedArtifact("Game/A", Path.Combine("C:", "out", "Game", "A.json"));

        recorder.AddArtifacts([artifact, artifact]);
        recorder.EndSource(SourceStatus.Ok);

        Assert.Single(Assert.Single(recorder.Records).Outputs);
    }

    [Fact]
    public void MarkExternalWwise_SetsTheFlagOnTheCurrentSourceOnly()
    {
        var recorder = NewRecorder();
        recorder.BeginSource("Game/A.uasset", ["Game/A.uasset"]);
        recorder.MarkExternalWwise();
        recorder.EndSource(SourceStatus.Ok);
        recorder.BeginSource("Game/B.uasset", ["Game/B.uasset"]);
        recorder.EndSource(SourceStatus.Ok);

        Assert.True(recorder.Records[0].ExternalWwise);
        Assert.False(recorder.Records[1].ExternalWwise);
    }

    [Fact]
    public void AddMediaDependency_LandsInDependencies()
    {
        var recorder = NewRecorder();
        recorder.BeginSource("Game/Bank.uasset", ["Game/Bank.uasset"]);

        recorder.AddMediaDependency("Game/Sound.wem");
        recorder.EndSource(SourceStatus.Ok);

        Assert.Equal(["Game/Sound.wem"], Assert.Single(recorder.Records).Dependencies);
    }

    [Fact]
    public void AddMediaDependency_KeepsOrdinallyDistinctPathsThatCultureCollationTreatsAsEqual()
    {
        // Precomposed "e-acute" (one UTF-16 code unit) and "e" followed by a combining acute
        // accent (two code units) are ordinally distinct, but the default, culture-sensitive
        // string comparer treats them as equal under every culture, including invariant. A
        // SortedSet built with that default comparer would silently collapse the two into one
        // entry, losing a real dependency edge. Ordinal must not do that.
        var recorder = NewRecorder();
        recorder.BeginSource("Game/Bank.uasset", ["Game/Bank.uasset"]);

        var precomposed = "Game/Café.wem";
        var decomposed = "Game/Café.wem";

        recorder.AddMediaDependency(precomposed);
        recorder.AddMediaDependency(decomposed);
        recorder.EndSource(SourceStatus.Ok);

        var dependencies = Assert.Single(recorder.Records).Dependencies;
        Assert.Equal(2, dependencies.Count);
        Assert.Contains(precomposed, dependencies);
        Assert.Contains(decomposed, dependencies);
    }

    [Fact]
    public void EndSource_FlagOffAndSourceReExported_RecordsUnknownBytecode()
    {
        // The flag was off, so serializedScriptSize was read into a local and discarded. The
        // content changed and we could not look, so the only honest state is unknown.
        var recorder = NewRecorder(scriptBytecode: false);
        recorder.BeginSource("Game/A.uasset", ["Game/A.uasset"]);

        recorder.EndSource(SourceStatus.Ok);

        Assert.Equal(BytecodeState.Unknown, Assert.Single(recorder.Records).Bytecode);
    }

    [Fact]
    public void EndSource_NonJsonMode_RecordsFalseBytecode()
    {
        // --script-bytecode is ignored outside json mode, so it can never make output differ and
        // must never invalidate anything.
        var recorder = NewRecorder(scriptBytecode: false, isJsonMode: false);
        recorder.BeginSource("Game/A.uasset", ["Game/A.uasset"]);

        recorder.EndSource(SourceStatus.Ok);

        Assert.Equal(BytecodeState.False, Assert.Single(recorder.Records).Bytecode);
    }

    [Fact]
    public void EndSource_StatusIsRecorded()
    {
        var recorder = NewRecorder();
        recorder.BeginSource("Game/A.uasset", ["Game/A.uasset"]);

        recorder.EndSource(SourceStatus.Failed);

        Assert.Equal(SourceStatus.Failed, Assert.Single(recorder.Records).Status);
    }

    [Fact]
    public void EndSource_WithoutBeginSource_Throws()
    {
        var recorder = NewRecorder();

        Assert.Throws<InvalidOperationException>(() => recorder.EndSource(SourceStatus.Ok));
    }

    // Covers the hard invariant a gap in which never heals: the leaf names in ClrTypes and the keys
    // of ClrTypeChains must be exactly the same set. A leaf missing from ClrTypes is the harmful
    // direction, not merely a subset check away from safe: the skip predicate then evaluates "every
    // export type is specialized" over fewer leaves than were actually exported, which makes it more
    // likely to come out true, so a package can be judged skipped when a full evaluation would not
    // have skipped it. A leaf missing from ClrTypeChains falls back to a leaf-only chain and never
    // gets a chance to correct itself. ObservePackage itself needs a live package, so this drives
    // the pure function it delegates to directly, with plain typeof(...) values standing in for
    // export types.
    [Fact]
    public void BuildClrTypeInfo_ClrTypesAndClrTypeChainKeysAreExactlyTheSameSet()
    {
        var (clrTypes, clrTypeChains) = SourceRecorder.BuildClrTypeInfo([typeof(LeafTypeA), typeof(LeafTypeB), typeof(LeafTypeA)]);

        Assert.Equal(clrTypes.Order(), clrTypeChains.Keys.Order());
    }

    [Fact]
    public void BuildClrTypeInfo_ChainMatchesIsSpecializedsOwnWalk()
    {
        var (_, clrTypeChains) = SourceRecorder.BuildClrTypeInfo([typeof(LeafTypeA)]);

        Assert.Equal([nameof(LeafTypeA), nameof(BaseType)], clrTypeChains[nameof(LeafTypeA)]);
    }

    private class BaseType;
    private sealed class LeafTypeA : BaseType;
    private sealed class LeafTypeB : BaseType;
}
