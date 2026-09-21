using System.Globalization;
using System.Text;
using CUE4Parse.UE4.Assets.Exports;
using Newtonsoft.Json;
using UnrealAssetScout.Export;
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

    [Fact]
    public void ShouldSkipByClassName_MatchesABaseTypeOfTheResolvedClass()
    {
        // AnimSequence resolves to UAnimSequence, whose base UAnimSequenceBase is what the built-in
        // list names. This is the export-map path, so no export is deserialized to decide it.
        var shouldSkip = JsonPackageProcessor.ShouldSkipByClassName(
            ["AnimSequence"],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "UAnimSequenceBase" });

        Assert.True(shouldSkip);
    }

    [Fact]
    public void ShouldSkipByClassName_IsFalseWhenAClassNameDoesNotResolve()
    {
        // A blueprint class is not in the registry. Deciding here would skip a package that the
        // loaded-object check would have kept, so an unresolved name has to defer.
        var shouldSkip = JsonPackageProcessor.ShouldSkipByClassName(
            ["SomeBlueprintClassThatIsNotRegistered_C"],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "UObject" });

        Assert.False(shouldSkip);
    }

    [Fact]
    public void ShouldSkipByClassName_IsFalseWhenOnlySomeExportsAreSkipped()
    {
        var shouldSkip = JsonPackageProcessor.ShouldSkipByClassName(
            ["Texture2D", "AnimSequence"],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "UTexture" });

        Assert.False(shouldSkip);
    }

    [Fact]
    public void ShouldSkipByClassName_IsFalseForAnEmptyPackage()
    {
        var shouldSkip = JsonPackageProcessor.ShouldSkipByClassName(
            [],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "UTexture" });

        Assert.False(shouldSkip);
    }

    [Fact]
    public void ShouldSkipByClassName_IsFalseForAnEmptySkipList()
    {
        var shouldSkip = JsonPackageProcessor.ShouldSkipByClassName(
            ["Texture2D"],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        Assert.False(shouldSkip);
    }

    [Fact]
    public void ShouldSkipByClassName_IsFalseWhenAClassNameIsMissing()
    {
        var shouldSkip = JsonPackageProcessor.ShouldSkipByClassName(
            [null],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "UObject" });

        Assert.False(shouldSkip);
    }

    [Fact]
    public void TryExport_WritesTheSameBytesAsSerializingToAString()
    {
        // Output must not change, because exports are compared across runs. Run under a culture that
        // writes a decimal comma, so a culture leaking into number formatting would show up here.
        using var temp = new TempDir();
        UObject[] exports = [new NumericExport(), new NumericExport()];
        var expected = new UTF8Encoding(false).GetBytes(JsonConvert.SerializeObject(exports, Formatting.Indented));

        var previousCulture = CultureInfo.CurrentCulture;
        ExportAttemptResult result;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            result = PackageJsonExporter.TryExport("Game/Content/Asset.uasset", temp.Path, exports);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }

        Assert.True(result.Succeeded);
        Assert.Equal(expected, File.ReadAllBytes(result.ExportedArtifacts[0].OutputPath));
    }

    [Fact]
    public void TryExport_LeavesNoFileWhenSerializationFails()
    {
        // A failed package records no output in the manifest, so a file left behind would never be
        // cleaned up by a later run, and a truncated one would read as a valid export.
        using var temp = new TempDir();
        var outputPath = ExportPathUtils.ToOutputPath(temp.Path, "Game/Content/Broken.uasset", ".json");

        var result = PackageJsonExporter.TryExport("Game/Content/Broken.uasset", temp.Path, [new ThrowingExport()]);

        Assert.True(result.Failed);
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public void TryExport_FailsOnAnUnpairedSurrogateRatherThanRewritingIt()
    {
        // The JSON encoder rejects invalid UTF-16. A lenient encoder would instead write U+FFFD and
        // report success, changing the bytes of any package carrying a malformed string.
        using var temp = new TempDir();

        var result = PackageJsonExporter.TryExport("Game/Content/Surrogate.uasset", temp.Path, [new UnpairedSurrogateExport()]);

        Assert.True(result.Failed);
    }

    private class BaseSkippedType : UObject;

    private class DerivedSkippedType : BaseSkippedType;

    private sealed class GrandchildSkippedType : DerivedSkippedType;

    private sealed class RetainedType : UObject;

    private sealed class NumericExport : UObject
    {
        protected override void WriteJson(JsonWriter writer, JsonSerializer serializer)
        {
            base.WriteJson(writer, serializer);
            writer.WritePropertyName("Single");
            writer.WriteValue(1.5f);
            writer.WritePropertyName("Double");
            writer.WriteValue(-12345.6789d);
            writer.WritePropertyName("Large");
            writer.WriteValue(1.0e21d);
        }
    }

    private sealed class UnpairedSurrogateExport : UObject
    {
        protected override void WriteJson(JsonWriter writer, JsonSerializer serializer)
        {
            base.WriteJson(writer, serializer);
            writer.WritePropertyName("Text");
            writer.WriteValue("before\uD800after");
        }
    }

    private sealed class ThrowingExport : UObject
    {
        protected override void WriteJson(JsonWriter writer, JsonSerializer serializer)
        {
            base.WriteJson(writer, serializer);
            writer.WritePropertyName("Partial");
            writer.WriteValue("written before the failure");
            throw new InvalidOperationException("serialization failed part way");
        }
    }
}
