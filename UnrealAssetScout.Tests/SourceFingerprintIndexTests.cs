using System;
using System.Collections.Generic;
using System.Linq;
using CUE4Parse.Compression;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.FileProvider.Vfs;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Readers;
using UnrealAssetScout.Incremental;

namespace UnrealAssetScout.Tests;

public sealed class SourceFingerprintIndexTests
{
    [Fact]
    public void FromEntries_MapsEveryPathToItsHash()
    {
        var index = SourceFingerprintIndex.FromEntries(
        [
            ("Game/A.uasset", "aaaa"),
            ("Game/A.uexp", "bbbb")
        ]);

        Assert.Equal("aaaa", index.ByPath["Game/A.uasset"]);
        Assert.Equal("bbbb", index.ByPath["Game/A.uexp"]);
        Assert.Equal(0, index.UnfingerprintedCount);
    }

    [Fact]
    public void ByPath_LooksUpCaseInsensitively()
    {
        // The provider is always configured with StringComparer.OrdinalIgnoreCase.
        // IncrementalRunner.ResolvePackagePath's FilesById leg resolves a "packageid:" identity
        // through the provider's own by-id index, which is not guaranteed to hand back a path in
        // the exact same casing this table was built with. A stricter comparer would silently fail
        // that lookup, so the dependency's fingerprint would never be recorded under its identity.
        var index = SourceFingerprintIndex.FromEntries([("Game/A.uasset", "aaaa")]);

        Assert.Equal("aaaa", index.ByPath["GAME/A.UASSET"]);
    }

    [Fact]
    public void FromEntries_CountsEntriesWithNoHash()
    {
        var index = SourceFingerprintIndex.FromEntries(
        [
            ("Game/A.uasset", "aaaa"),
            ("Game/B.uasset", null)
        ]);

        Assert.Single(index.ByPath);
        Assert.Equal(1, index.UnfingerprintedCount);
    }

    [Fact]
    public void ResolvedFiles_MatchesTheProvidersOwnResolutionForAShadowedPath()
    {
        // FileProviderDictionary.Values enumerates every mounted container's entries in descending
        // read order, so a patch container's entry for a shadowed path comes out before the base
        // container's -- verified here against the real dictionary, not assumed. A last-write-wins
        // fold over that sequence would therefore record the base entry, which is neither the
        // provider's resolution nor the file the exporter reads.
        var provider = new FakeVfsProvider(StringComparer.OrdinalIgnoreCase);
        var basePak = new FakeGameFile("Game/A.uasset");
        var patchPak = new FakeGameFile("Game/A.uasset");

        provider.Files.AddFiles(new Dictionary<string, GameFile> { ["Game/A.uasset"] = basePak }, readOrder: 3);
        provider.Files.AddFiles(new Dictionary<string, GameFile> { ["Game/A.uasset"] = patchPak }, readOrder: 103);

        Assert.Equal([patchPak, basePak], provider.Files.Values);

        // Reproduces the pre-fix defect concretely: folding the real enumeration order straight
        // through FromEntries's last-write-wins, as Build used to, records the base entry.
        var preFixResult = SourceFingerprintIndex.FromEntries(provider.Files.Values.Select(file =>
            (file.Path, Fingerprint: (string?) (ReferenceEquals(file, patchPak) ? "patch-fingerprint" : "base-fingerprint"))));
        Assert.Equal("base-fingerprint", preFixResult.ByPath["Game/A.uasset"]);

        // What Build must record instead: the entry the provider itself resolves for the path.
        Assert.Same(patchPak, provider.Files["Game/A.uasset"]);
        Assert.Same(patchPak, Assert.Single(SourceFingerprintIndex.ResolvedFiles(provider)));
    }

    private sealed class FakeVfsProvider : AbstractVfsFileProvider
    {
        internal FakeVfsProvider(StringComparer pathComparer) : base(pathComparer: pathComparer) { }

        public override void Initialize() { }
    }

    private sealed class FakeGameFile : GameFile
    {
        internal FakeGameFile(string path) : base(path, 0) { }

        public override bool IsEncrypted => false;
        public override CompressionMethod CompressionMethod => CompressionMethod.None;
        public override byte[] Read(FByteBulkDataHeader? header = null) => throw new NotImplementedException();
        public override FArchive CreateReader(FByteBulkDataHeader? header = null) => throw new NotImplementedException();
    }
}
