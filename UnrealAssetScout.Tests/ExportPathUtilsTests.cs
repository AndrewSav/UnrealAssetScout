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
    public void ApplyOriginSuffix_AnOriginWithAContainer_AppendsTheContainersOwnName()
    {
        var name = ExportPathUtils.ApplyOriginSuffix(
            "AKE_Footsteps_Creak_1 (SFX)",
            new ArtifactOrigin("Game/WwiseAudio/Media/350661223.wem", "AKE_Footsteps_Creak_1 (SFX)"));

        Assert.Equal("AKE_Footsteps_Creak_1 (SFX) [350661223]", name);
    }

    [Fact]
    public void ApplyOriginSuffix_TwoContainersSharingAName_ProduceDifferentNames()
    {
        // The defect this exists to remove: two media whose Wwise debug name matches land on one
        // file, because the name CUE4Parse builds carries the language rather than the media.
        var first = ExportPathUtils.ApplyOriginSuffix(
            "Creak_1 (SFX)", new ArtifactOrigin("Game/WwiseAudio/Media/350661223.wem", "Creak_1 (SFX)"));
        var second = ExportPathUtils.ApplyOriginSuffix(
            "Creak_1 (SFX)", new ArtifactOrigin("Game/WwiseAudio/Media/884337675.wem", "Creak_1 (SFX)"));

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ApplyOriginSuffix_OneContainerReachedTwice_KeepsBothOnOneName()
    {
        var first = ExportPathUtils.ApplyOriginSuffix(
            "Wind (SFX)", new ArtifactOrigin("Game/WwiseAudio/Media/26426.wem", "Wind (SFX)"));
        var second = ExportPathUtils.ApplyOriginSuffix(
            "Wind (SFX)", new ArtifactOrigin("Game/WwiseAudio/Media/26426.wem", "Wind (SFX)"));

        Assert.Equal(first, second);
    }

    [Fact]
    public void ApplyOriginSuffix_NoOrigin_LeavesTheNameAlone()
    {
        var name = ExportPathUtils.ApplyOriginSuffix("Wind (SFX)", null);

        Assert.Equal("Wind (SFX)", name);
    }

    [Fact]
    public void ApplyOriginSuffix_ANameThatIsItselfAPath_SuffixesOnlyTheLastSegment()
    {
        var name = ExportPathUtils.ApplyOriginSuffix(
            "Music/Intro", new ArtifactOrigin("Game/WwiseAudio/Media/26426.wem", "Music/Intro"));

        Assert.Equal("Music/Intro [26426]", name);
    }

    [Fact]
    public void ComposeExportPath_LeafWithCharactersIllegalInAFileName_IsSanitised()
    {
        var path = ExportPathUtils.ComposeExportPath("Game/Folder/Foo.uasset", "Bar:Baz", nestUnderPackage: true);

        Assert.Equal("Game/Folder/Foo/Bar_Baz", path);
    }
}
