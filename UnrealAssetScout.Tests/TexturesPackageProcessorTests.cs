using CUE4Parse.GameTypes.KRD.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Texture;
using UnrealAssetScout.Export.Processors;

namespace UnrealAssetScout.Tests;

public sealed class TexturesPackageProcessorTests
{
    [Fact]
    public void WritesMoreThanOneFile_APackageWithOneTexture_DoesNotNest()
    {
        var nest = TexturesPackageProcessor.WritesMoreThanOneFile([new UTexture()]);

        Assert.False(nest);
    }

    [Fact]
    public void WritesMoreThanOneFile_APackageWithTwoTextures_Nests()
    {
        var nest = TexturesPackageProcessor.WritesMoreThanOneFile([new UTexture(), new UTexture()]);

        Assert.True(nest);
    }

    [Fact]
    public void WritesMoreThanOneFile_ExportsThisModeDoesNotWrite_DoNotCountTowardsNesting()
    {
        // A map package holds far more than its textures, so counting every export would nest the
        // single-texture case that the flat path exists for.
        var nest = TexturesPackageProcessor.WritesMoreThanOneFile([new UTexture(), new UObject()]);

        Assert.False(nest);
    }

    [Fact]
    public void WritesMoreThanOneFile_ATextureBesideAnSvgAsset_Nests()
    {
        var nest = TexturesPackageProcessor.WritesMoreThanOneFile([new UTexture(), new USvgAsset()]);

        Assert.True(nest);
    }
}
