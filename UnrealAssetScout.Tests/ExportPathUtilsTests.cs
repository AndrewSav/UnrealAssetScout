using UnrealAssetScout.Export;

namespace UnrealAssetScout.Tests;

public sealed class ExportPathUtilsTests
{
    [Fact]
    public void ComposeExportPath_WhenNotNested_NamesTheOutputAfterThePackageRatherThanTheExport()
    {
        var path = ExportPathUtils.ComposeExportPath("Game/Folder/Foo.uasset", "Bar", nestUnderPackage: false);

        Assert.Equal("Game/Folder/Foo", path);
    }

    [Fact]
    public void ComposeExportPath_WhenNested_PlacesTheLeafBeneathThePackageName()
    {
        var path = ExportPathUtils.ComposeExportPath("Game/Folder/Foo.uasset", "Bar", nestUnderPackage: true);

        Assert.Equal("Game/Folder/Foo/Bar", path);
    }

    [Fact]
    public void ComposeExportPath_SiblingPackagesSharingAnExportName_DoNotCollide()
    {
        // World-partition maps in one directory each carry exports named Texture2D_0, which is the
        // shape that made the old package-directory-plus-export-name path non-injective.
        var first = ExportPathUtils.ComposeExportPath("Game/Folder/First.umap", "Texture2D_0", nestUnderPackage: true);
        var second = ExportPathUtils.ComposeExportPath("Game/Folder/Second.umap", "Texture2D_0", nestUnderPackage: true);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ComposeExportPath_FlatPathsOfSiblingPackages_DoNotCollide()
    {
        var first = ExportPathUtils.ComposeExportPath("Game/Folder/First.uasset", "Shared", nestUnderPackage: false);
        var second = ExportPathUtils.ComposeExportPath("Game/Folder/Second.uasset", "Shared", nestUnderPackage: false);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ComposeExportPath_LeafContainingSeparators_BecomesNestedDirectories()
    {
        var path = ExportPathUtils.ComposeExportPath("Game/Folder/Bank.uasset", "Music/Intro", nestUnderPackage: true);

        Assert.Equal("Game/Folder/Bank/Music/Intro", path);
    }

    [Fact]
    public void ComposeExportPath_PackageAtTheRoot_HasNoLeadingSeparator()
    {
        var path = ExportPathUtils.ComposeExportPath("Foo.uasset", "Bar", nestUnderPackage: true);

        Assert.Equal("Foo/Bar", path);
    }

    [Fact]
    public void ComposeExportPath_LeafWithCharactersIllegalInAFileName_IsSanitised()
    {
        var path = ExportPathUtils.ComposeExportPath("Game/Folder/Foo.uasset", "Bar:Baz", nestUnderPackage: true);

        Assert.Equal("Game/Folder/Foo/Bar_Baz", path);
    }
}
