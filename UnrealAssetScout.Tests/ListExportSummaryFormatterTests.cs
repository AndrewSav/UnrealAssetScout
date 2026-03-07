using UnrealAssetScout.Package;

namespace UnrealAssetScout.Tests;

public class ListExportSummaryFormatterTests
{
    [Fact]
    public void FormatPackageExports_GroupsByTypeIntoCsvRows()
    {
        var rows = ListExportSummaryFormatter.FormatPackageExports("Project/Content/TestAsset.uasset",
        [
            "USvgAsset",
            "UTexture",
            "UTexture"
        ]);

        Assert.Equal(
        [
            "Project/Content/TestAsset.uasset,USvgAsset,1",
            "Project/Content/TestAsset.uasset,UTexture,2"
        ], rows);
    }

    [Fact]
    public void FormatPackageExports_WithNoExports_ReturnsSingleEmptyTypeRow()
    {
        var rows = ListExportSummaryFormatter.FormatPackageExports("Project/Content/EmptyAsset.uasset", []);

        Assert.Equal(["Project/Content/EmptyAsset.uasset,,"], rows);
    }

    [Fact]
    public void FormatPackageExports_WhenMappingsAreRequired_ReturnsSingleEmptyTypeRow()
    {
        var packageContext = new PackageExportContext(null, "Project/Content/TestAsset.uasset",
            UsmapRequirement.Unknown, string.Empty, PackageLoadResult.FailureRequiresUsmap);

        var rows = ListExportSummaryFormatter.FormatPackageExports("Project/Content/TestAsset.uasset", packageContext);

        Assert.Equal(["Project/Content/TestAsset.uasset,,"], rows);
    }

    [Fact]
    public void FormatPackageExports_WhenPackageLoadFails_ReturnsSingleEmptyTypeRow()
    {
        var packageContext = new PackageExportContext(null, "Project/Content/TestAsset.uasset",
            UsmapRequirement.Unknown, string.Empty, PackageLoadResult.FailureOther);

        var rows = ListExportSummaryFormatter.FormatPackageExports("Project/Content/TestAsset.uasset", packageContext);

        Assert.Equal(["Project/Content/TestAsset.uasset,,"], rows);
    }

    [Fact]
    public void FormatNoExports_ReturnsSingleEmptyTypeRow()
    {
        Assert.Equal(["Project/Content/readme.txt,,"], ListExportSummaryFormatter.FormatNoExports("Project/Content/readme.txt"));
    }

    [Fact]
    public void FormatPackageExports_EscapesCsvFields()
    {
        var rows = ListExportSummaryFormatter.FormatPackageExports("Project/Content/Ui,\"Hud\".uasset",
        [
            "Type,One"
        ]);

        Assert.Equal(["\"Project/Content/Ui,\"\"Hud\"\".uasset\",\"Type,One\",1"], rows);
    }
}
