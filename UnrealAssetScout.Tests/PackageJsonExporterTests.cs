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

    private class BaseSkippedType : UObject;

    private sealed class DerivedSkippedType : BaseSkippedType;
}
