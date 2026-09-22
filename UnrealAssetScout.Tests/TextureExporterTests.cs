using System.Reflection;
using System.Runtime.CompilerServices;
using CUE4Parse.UE4.Assets.Exports.Texture;
using UnrealAssetScout.Export;
using UnrealAssetScout.Export.Exporters;
using UnrealAssetScout.Package;

namespace UnrealAssetScout.Tests;

public sealed class TextureExporterTests
{
    [Fact]
    public void TryExport_RenderTargetWithNoImageData_HasNothingToExport()
    {
        using var temp = new TempDir();

        var result = TextureExporter.TryExport(new UTextureRenderTarget2D(), Context(), temp.Path, nestUnderPackage: false);

        Assert.Equal(ExportAttemptStatus.NotHandled, result.Status);
    }

    [Fact]
    public void TryExport_MediaTextureWithNoImageData_HasNothingToExport()
    {
        using var temp = new TempDir();

        var result = TextureExporter.TryExport(new UMediaTexture(), Context(), temp.Path, nestUnderPackage: false);

        Assert.Equal(ExportAttemptStatus.NotHandled, result.Status);
    }

    [Fact]
    public void TryExport_TextureWithMipsThatCannotBeDecoded_IsAFailure()
    {
        // A mip with no bulk data behind it: the texture does store image data, so failing to decode
        // it is a real failure, not an empty texture.
        using var temp = new TempDir();
        var texture = new UTexture2D();
        texture.PlatformData.Mips = [new FTexture2DMipMap(4, 4, 1)];

        var result = TextureExporter.TryExport(texture, Context(), temp.Path, nestUnderPackage: false);

        Assert.True(result.Failed);
    }

    [Fact]
    public void TryExport_VirtualTextureWithoutMips_IsStillAttempted()
    {
        // A virtual texture keeps its image data in VTData rather than in Mips, so an empty Mips
        // array alone must not make it look like a texture with nothing stored.
        using var temp = new TempDir();
        var texture = new UTexture2D();
        typeof(FTexturePlatformData)
            .GetField(nameof(FTexturePlatformData.VTData), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(texture.PlatformData, RuntimeHelpers.GetUninitializedObject(typeof(FVirtualTextureBuiltData)));

        var result = TextureExporter.TryExport(texture, Context(), temp.Path, nestUnderPackage: false);

        Assert.True(result.Failed);
    }

    private static PackageExportContext Context() =>
        new(null, "Game/Textures/T_Example.uasset", UsmapRequirement.Unknown, "", PackageLoadResult.Success);
}
