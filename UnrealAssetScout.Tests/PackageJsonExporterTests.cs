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
    public void ShouldSkipJsonExport_MatchesBaseTypeName()
    {
        // The built-in list names categories as well as concrete types: UTexture, UAnimSequenceBase and
        // ALandscapeProxy are abstract bases that nothing is ever an instance of. Matching only the concrete
        // name left those entries doing nothing at all.
        var shouldSkip = JsonPackageProcessor.ShouldSkipJsonExport(
            [new DerivedSkippedType()],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { nameof(BaseSkippedType) });

        Assert.True(shouldSkip);
    }

    [Fact]
    public void ShouldSkipJsonExport_DoesNotMatchAnUnrelatedTypeName()
    {
        var shouldSkip = JsonPackageProcessor.ShouldSkipJsonExport(
            [new DerivedSkippedType()],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { nameof(RetainedType) });

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
    public void ShouldSkipJsonExport_MatchesThroughAWholeInheritanceChain()
    {
        // UTexture -> UTexture2D is one hop; deeper chains must work the same way.
        var shouldSkip = JsonPackageProcessor.ShouldSkipJsonExport(
            [new GrandchildSkippedType()],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { nameof(BaseSkippedType) });

        Assert.True(shouldSkip);
    }

    private class BaseSkippedType : UObject;

    private class DerivedSkippedType : BaseSkippedType;

    private sealed class GrandchildSkippedType : DerivedSkippedType;

    private sealed class RetainedType : UObject;
}
