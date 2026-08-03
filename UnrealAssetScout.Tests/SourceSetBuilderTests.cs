using System.Text.RegularExpressions;
using UnrealAssetScout.Export;
using UnrealAssetScout.Incremental;

namespace UnrealAssetScout.Tests;

public sealed class SourceSetBuilderTests
{
    private static readonly string[] Files =
    [
        "Game/Content/A.uasset", "Game/Content/A.uexp", "Game/Content/A.ubulk", "Game/Content/A.uptnl",
        "Game/Content/B.umap", "Game/Content/B.uexp",
        "Game/Content/Sound.wem", "Game/Content/Config.ini"
    ];

    [Fact]
    public void Build_PackageMode_KeysOnPackagesAndFoldsPayloadsIntoConstituents()
    {
        var sources = SourceSetBuilder.Build(Files, ExportMode.Json, filter: null, typeFilteredPaths: null);

        Assert.Equal(["Game/Content/A.uasset", "Game/Content/B.umap"], sources.Keys.Order());
        Assert.Equal(
            ["Game/Content/A.uasset", "Game/Content/A.uexp", "Game/Content/A.ubulk", "Game/Content/A.uptnl"],
            sources["Game/Content/A.uasset"].Constituents);
    }

    [Fact]
    public void Build_PackageMode_IncludesTheUassetItselfAsAConstituent()
    {
        var sources = SourceSetBuilder.Build(Files, ExportMode.Textures, null, null);

        Assert.Contains("Game/Content/A.uasset", sources["Game/Content/A.uasset"].Constituents);
    }

    [Fact]
    public void Build_VerseMode_TakesUassetOnly()
    {
        var sources = SourceSetBuilder.Build(Files, ExportMode.Verse, null, null);

        Assert.Equal(["Game/Content/A.uasset"], sources.Keys);
    }

    [Fact]
    public void Build_SimpleMode_TakesEverythingThatIsNotAPackageOrPayload()
    {
        var sources = SourceSetBuilder.Build(Files, ExportMode.Simple, null, null);

        Assert.Equal(["Game/Content/Config.ini", "Game/Content/Sound.wem"], sources.Keys.Order());
        Assert.Equal(["Game/Content/Sound.wem"], sources["Game/Content/Sound.wem"].Constituents);
    }

    [Fact]
    public void Build_RawMode_MakesEveryFileItsOwnSource()
    {
        var sources = SourceSetBuilder.Build(Files, ExportMode.Raw, null, null);

        Assert.Equal(Files.Length, sources.Count);
        Assert.Equal(["Game/Content/A.uexp"], sources["Game/Content/A.uexp"].Constituents);
    }

    [Fact]
    public void Build_Filter_NarrowsByTheSourcePath()
    {
        var sources = SourceSetBuilder.Build(Files, ExportMode.Json, new Regex("B\\.umap$"), null);

        Assert.Equal(["Game/Content/B.umap"], sources.Keys);
    }

    [Fact]
    public void Build_FilterExcludingAPackage_StillDropsItsPayloads()
    {
        var sources = SourceSetBuilder.Build(Files, ExportMode.Json, new Regex("B\\.umap$"), null);

        Assert.DoesNotContain(sources.Values, source => source.Constituents.Contains("Game/Content/A.uexp"));
    }

    [Fact]
    public void Build_TypeFilteredPaths_NarrowsFurther()
    {
        var sources = SourceSetBuilder.Build(
            Files, ExportMode.Json, null, new HashSet<string> { "Game/Content/A.uasset" });

        Assert.Equal(["Game/Content/A.uasset"], sources.Keys);
    }

    [Fact]
    public void Build_PackageWithNoPayloads_HasItselfAsItsOnlyConstituent()
    {
        var sources = SourceSetBuilder.Build(["Game/Content/Lone.uasset"], ExportMode.Json, null, null);

        Assert.Equal(["Game/Content/Lone.uasset"], sources["Game/Content/Lone.uasset"].Constituents);
    }

    [Fact]
    public void Build_PayloadWithNoOwningPackage_IsNotASourceInPackageModes()
    {
        var sources = SourceSetBuilder.Build(["Game/Content/Orphan.uexp"], ExportMode.Json, null, null);

        Assert.Empty(sources);
    }
}
