using CUE4Parse.FileProvider.Objects;
using UnrealAssetScout.Utils;

namespace UnrealAssetScout.Tests;

public sealed class ProviderFilesTests
{
    [Fact]
    public void Resolved_ShadowedPath_YieldsOnlyTheEntryTheProviderResolves()
    {
        var provider = new FakeVfsProvider(StringComparer.OrdinalIgnoreCase);
        var basePak = new FakeGameFile("Game/A.uasset");
        var patchPak = new FakeGameFile("Game/A.uasset");

        provider.Files.AddFiles(new Dictionary<string, GameFile> { ["Game/A.uasset"] = basePak }, readOrder: 3);
        provider.Files.AddFiles(new Dictionary<string, GameFile> { ["Game/A.uasset"] = patchPak }, readOrder: 103);

        Assert.Equal([patchPak, basePak], provider.Files.Values);
        Assert.Same(patchPak, provider.Files["Game/A.uasset"]);
        Assert.Same(patchPak, Assert.Single(ProviderFiles.Resolved(provider)));
    }

    [Fact]
    public void Resolved_PathsInDifferentContainers_YieldsEachOnce()
    {
        var provider = new FakeVfsProvider(StringComparer.OrdinalIgnoreCase);
        provider.Files.AddFiles(new Dictionary<string, GameFile> { ["Game/A.uasset"] = new FakeGameFile("Game/A.uasset") }, readOrder: 3);
        provider.Files.AddFiles(new Dictionary<string, GameFile> { ["Game/B.uasset"] = new FakeGameFile("Game/B.uasset") }, readOrder: 103);

        Assert.Equal(["Game/A.uasset", "Game/B.uasset"], ProviderFiles.Resolved(provider).Select(file => file.Path).Order());
    }
}
