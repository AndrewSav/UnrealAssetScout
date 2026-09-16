using System.Linq;
using UnrealAssetScout.Export;
using UnrealAssetScout.Incremental;

namespace UnrealAssetScout.Tests;

public sealed class DuplicateOutputCheckTests
{
    [Fact]
    public void FindOutputsWithMoreThanOneOrigin_EachArtifactWritingItsOwnFile_FindsNothing()
    {
        ExportedArtifact[] artifacts =
        [
            new("Game/A.uasset/A", "out/Game/A.png"),
            new("Game/B.uasset/B", "out/Game/B.png")
        ];

        Assert.Empty(DuplicateOutputCheck.FindOutputsWithMoreThanOneOrigin(artifacts));
    }

    [Fact]
    public void FindOutputsWithMoreThanOneOrigin_OnePakEntryReachedByTwoSources_FindsNothing()
    {
        // Wwise media referenced by several events: both writes carry the same bytes out of the same
        // pak entry, so one file is the right answer and nothing is lost by them sharing it.
        ExportedArtifact[] artifacts =
        [
            new("Game/EventA.uasset/EventA", "out/Media/Wind.wem", new ArtifactOrigin("Game/WwiseAudio/Media/2641.wem", "Wind (SFX)")),
            new("Game/EventB.uasset/EventB", "out/Media/Wind.wem", new ArtifactOrigin("Game/WwiseAudio/Media/2641.wem", "Wind (SFX)"))
        ];

        Assert.Empty(DuplicateOutputCheck.FindOutputsWithMoreThanOneOrigin(artifacts));
    }

    [Fact]
    public void FindOutputsWithMoreThanOneOrigin_TwoPakEntriesWritingOneFile_NamesThatFile()
    {
        // Different data landing on one name. Whether the bytes match today is incidental: nothing
        // keeps two separate pak entries in step, so the file's content depends on write order.
        ExportedArtifact[] artifacts =
        [
            new("Game/EventA.uasset/EventA", "out/Media/Wind.wem", new ArtifactOrigin("Game/WwiseAudio/Media/2641.wem", "Wind (SFX)")),
            new("Game/EventB.uasset/EventB", "out/Media/Wind.wem", new ArtifactOrigin("Game/WwiseAudio/Media/9917.wem", "Wind (SFX)"))
        ];

        var conflict = Assert.Single(DuplicateOutputCheck.FindOutputsWithMoreThanOneOrigin(artifacts));

        Assert.Equal("out/Media/Wind.wem", conflict.Output);
    }

    [Fact]
    public void FindOutputsWithMoreThanOneOrigin_NamesTheOriginsThatWroteTheFile()
    {
        ExportedArtifact[] artifacts =
        [
            new("Game/EventA.uasset/EventA", "out/Media/Wind.wem", new ArtifactOrigin("Game/WwiseAudio/Media/2641.wem", "Wind (SFX)")),
            new("Game/EventB.uasset/EventB", "out/Media/Wind.wem", new ArtifactOrigin("Game/WwiseAudio/Media/9917.wem", "Wind (SFX)"))
        ];

        var conflict = Assert.Single(DuplicateOutputCheck.FindOutputsWithMoreThanOneOrigin(artifacts));

        Assert.Equal(
            ["Game/WwiseAudio/Media/2641.wem", "Game/WwiseAudio/Media/9917.wem"],
            conflict.Origins.Select(origin => origin.Container));
    }

    [Fact]
    public void FindOutputsWithMoreThanOneOrigin_ArtifactsWithNoRecordedOrigin_FallBackToTheExportIdentity()
    {
        ExportedArtifact[] artifacts =
        [
            new("Game/Map.umap/Texture2D_0", "out/Game/Texture2D_0.png"),
            new("Game/Map.umap/Texture2D_1", "out/Game/Texture2D_0.png")
        ];

        var conflict = Assert.Single(DuplicateOutputCheck.FindOutputsWithMoreThanOneOrigin(artifacts));

        Assert.Equal("out/Game/Texture2D_0.png", conflict.Output);
    }

    [Fact]
    public void FindOutputsWithMoreThanOneOrigin_TheSameArtifactRecordedTwice_FindsNothing()
    {
        ExportedArtifact[] artifacts =
        [
            new("Game/A.uasset/A", "out/Game/A.png"),
            new("Game/A.uasset/A", "out/Game/A.png")
        ];

        Assert.Empty(DuplicateOutputCheck.FindOutputsWithMoreThanOneOrigin(artifacts));
    }
}
