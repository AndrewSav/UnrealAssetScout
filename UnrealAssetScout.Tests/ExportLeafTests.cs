using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Objects.UObject;
using UnrealAssetScout.Export;

namespace UnrealAssetScout.Tests;

public sealed class ExportLeafTests
{
    // Stands in for a real outer chain: ResolvedObject exposes Name and Outer without loading the
    // object behind them, which is the whole reason the leaf is built from it.
    private sealed class FakeResolvedObject(string name, ResolvedObject? outer) : ResolvedObject(null!)
    {
        public override FName Name { get; } = new(name);
        public override ResolvedObject? Outer { get; } = outer;
    }

    private static ResolvedObject Package() => new FakeResolvedObject("/Game/Folder/Foo", null);

    [Fact]
    public void ComposeExportLeaf_AnExportDirectlyInThePackage_IsJustItsName()
    {
        var texture = new UTexture { Name = "T_Icon", Outer = Package() };

        Assert.Equal("T_Icon", ExportPathUtils.ComposeExportLeaf(texture));
    }

    [Fact]
    public void ComposeExportLeaf_AnExportBeneathAnotherObject_IsPrefixedByThatOuter()
    {
        var component = new FakeResolvedObject("LandscapeComponent_3", Package());
        var texture = new UTexture { Name = "Heightmap_0", Outer = component };

        Assert.Equal("LandscapeComponent_3/Heightmap_0", ExportPathUtils.ComposeExportLeaf(texture));
    }

    [Fact]
    public void ComposeExportLeaf_TwoExportsSharingANameUnderDifferentOuters_DoNotCollide()
    {
        // Landscape components each own a Heightmap_0, so the export name alone is not unique even
        // within one package.
        var package = Package();
        var first = new UTexture { Name = "Heightmap_0", Outer = new FakeResolvedObject("Component_1", package) };
        var second = new UTexture { Name = "Heightmap_0", Outer = new FakeResolvedObject("Component_2", package) };

        Assert.NotEqual(ExportPathUtils.ComposeExportLeaf(first), ExportPathUtils.ComposeExportLeaf(second));
    }
}
