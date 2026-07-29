using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Objects.UObject;
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

    [Fact]
    public void FormatPackageExports_FromLoadedPackage_ReadsClassNamesFromExportMapWithoutDeserializing()
    {
        var packageContext = LoadedPackageContext(["SvgAsset", "Texture2D", "Texture2D"]);

        var rows = ListExportSummaryFormatter.FormatPackageExports("Project/Content/TestAsset.uasset", packageContext);

        Assert.Equal(
        [
            "Project/Content/TestAsset.uasset,SvgAsset,1",
            "Project/Content/TestAsset.uasset,Texture2D,2"
        ], rows);
    }

    [Fact]
    public void FormatPackageExports_FromLoadedPackage_WhenClassIsUnresolved_ReportsUObject()
    {
        var packageContext = LoadedPackageContext([null]);

        var rows = ListExportSummaryFormatter.FormatPackageExports("Project/Content/TestAsset.uasset", packageContext);

        Assert.Equal(["Project/Content/TestAsset.uasset,UObject,1"], rows);
    }

    [Fact]
    public void FormatPackageExports_FromLoadedPackageWithoutExports_ReturnsSingleEmptyTypeRow()
    {
        var packageContext = LoadedPackageContext([]);

        var rows = ListExportSummaryFormatter.FormatPackageExports("Project/Content/EmptyAsset.uasset", packageContext);

        Assert.Equal(["Project/Content/EmptyAsset.uasset,,"], rows);
    }

    private static PackageExportContext LoadedPackageContext(string?[] exportClassNames) =>
        new(new ExportMapOnlyPackage(exportClassNames), "Project/Content/TestAsset.uasset",
            UsmapRequirement.DoesNotRequireUsmap, string.Empty, PackageLoadResult.Success);

    // A package stand-in that exposes only an export map and fails loudly if any export is deserialized,
    // so tests can prove list --format types reads class names from the export map alone.
    private sealed class ExportMapOnlyPackage(string?[] exportClassNames) : IPackage
    {
        public string Name { get; set; } = "TestAsset";
        public IFileProvider? Provider => null;
        public TypeMappings? Mappings => null;
        public FPackageFileSummary Summary => new();
        public FNameEntrySerialized[] NameMap => [];
        public int ImportMapLength => 0;
        public int ExportMapLength => exportClassNames.Length;
        public bool IsFullyLoaded => false;
        public bool CanDeserialize => true;

        public Lazy<UObject>[] ExportsLazy { get; } = exportClassNames
            .Select(_ => new Lazy<UObject>(static () =>
                throw new InvalidOperationException("Export was deserialized")))
            .ToArray();

        public bool HasFlags(EPackageFlags flags) => false;

        public int GetExportIndex(string name, StringComparison comparisonType = StringComparison.Ordinal) => -1;

        public ResolvedObject? ResolvePackageIndex(FPackageIndex? index)
        {
            if (index is not { IsExport: true })
                return null;

            var className = exportClassNames[index.Index - 1];
            return new FakeResolvedObject(this, $"Export{index.Index}",
                className is null ? null : new FakeResolvedObject(this, className));
        }
    }

    // A minimal ResolvedObject used by ExportMapOnlyPackage to stand in for an export and its class.
    private sealed class FakeResolvedObject(IPackage package, string name, ResolvedObject? objectClass = null)
        : ResolvedObject(package)
    {
        public override FName Name { get; } = new(name);
        public override ResolvedObject? Class { get; } = objectClass;
    }
}
