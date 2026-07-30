using CUE4Parse.UE4.Assets.Exports;
using UnrealAssetScout.Export.Exporters;
using UnrealAssetScout.Export.Processors;

namespace UnrealAssetScout.Tests;

public sealed class PackageJsonExporterTests
{
    [Fact]
    public void ShouldSkipJsonExport_MatchesConcreteExportTypeName()
    {
        var shouldSkip = JsonPackageProcessor.ShouldSkipJsonExport(
            [new DerivedSkippedType()],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { nameof(DerivedSkippedType) });

        Assert.True(shouldSkip);
    }

    [Fact]
    public void ShouldSkipJsonExport_DoesNotMatchBaseTypeName()
    {
        var shouldSkip = JsonPackageProcessor.ShouldSkipJsonExport(
            [new DerivedSkippedType()],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { nameof(BaseSkippedType) });

        Assert.False(shouldSkip);
    }

    [Fact]
    public void ShouldSkipJsonExport_DoesNotMatchConcreteExportFullName()
    {
        var shouldSkip = JsonPackageProcessor.ShouldSkipJsonExport(
            [new DerivedSkippedType()],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { typeof(DerivedSkippedType).FullName! });

        Assert.False(shouldSkip);
    }

    [Fact]
    public void ShouldSkipJsonExport_IsFalseWhenOnlySomeExportsAreSkipped()
    {
        // A level package mixes inline meshes with the placed actors that are the point of exporting it.
        // Skipping the whole package because one export is specialized loses every actor in it.
        var shouldSkip = JsonPackageProcessor.ShouldSkipJsonExport(
            [new DerivedSkippedType(), new RetainedType()],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { nameof(DerivedSkippedType) });

        Assert.False(shouldSkip);
    }

    [Fact]
    public void ShouldSkipJsonExport_IsTrueWhenEveryExportIsSkipped()
    {
        var shouldSkip = JsonPackageProcessor.ShouldSkipJsonExport(
            [new DerivedSkippedType(), new DerivedSkippedType()],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { nameof(DerivedSkippedType) });

        Assert.True(shouldSkip);
    }

    [Fact]
    public void ShouldSkipJsonExport_IsFalseForAnEmptyPackage()
    {
        var shouldSkip = JsonPackageProcessor.ShouldSkipJsonExport(
            [],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { nameof(DerivedSkippedType) });

        Assert.False(shouldSkip);
    }

    [Fact]
    public void FilterJsonExports_DropsOnlyTheSkippedExports()
    {
        var retained = new RetainedType();

        var result = JsonPackageProcessor.FilterJsonExports(
            [new DerivedSkippedType(), retained, new DerivedSkippedType()],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { nameof(DerivedSkippedType) });

        Assert.Single(result);
        Assert.Same(retained, result[0]);
    }

    [Fact]
    public void FilterJsonExports_KeepsEverythingWhenTheSkipListIsEmpty()
    {
        var result = JsonPackageProcessor.FilterJsonExports(
            [new DerivedSkippedType(), new RetainedType()],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void FilterJsonExports_DoesNotMatchBaseTypeName()
    {
        var result = JsonPackageProcessor.FilterJsonExports(
            [new DerivedSkippedType()],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { nameof(BaseSkippedType) });

        Assert.Single(result);
    }

    private class BaseSkippedType : UObject;

    private sealed class DerivedSkippedType : BaseSkippedType;

    private sealed class RetainedType : UObject;
}
