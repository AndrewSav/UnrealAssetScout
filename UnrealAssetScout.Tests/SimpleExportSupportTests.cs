using System.Globalization;
using System.Text;
using UnrealAssetScout.Export;
using UnrealAssetScout.Export.Exporters;
using Newtonsoft.Json;

namespace UnrealAssetScout.Tests;

public sealed class SimpleExportSupportTests
{
    [Fact]
    public void TryExportJson_WritesTheSameBytesAsSerializingToAString()
    {
        // Output must not change, because exports are compared across runs. Run under a culture that
        // writes a decimal comma, so a culture leaking into number formatting would show up here.
        using var temp = new TempDir();
        var payload = new NumericPayload();
        var expected = new UTF8Encoding(false).GetBytes(JsonConvert.SerializeObject(payload, Formatting.Indented));

        var previousCulture = CultureInfo.CurrentCulture;
        ExportAttemptResult result;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            result = SimpleExportSupport.TryExportJson(Item("Game/Localization/Game.locres"), temp.Path, _ => payload);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }

        Assert.True(result.Succeeded);
        Assert.Equal(expected, File.ReadAllBytes(result.ExportedArtifacts[0].OutputPath));
    }

    [Fact]
    public void TryExportJson_LeavesNoFileWhenSerializationFails()
    {
        using var temp = new TempDir();
        var outputPath = ExportPathUtils.ToOutputPath(temp.Path, "Game/AssetRegistry.bin", ".json");

        var result = SimpleExportSupport.TryExportJson(Item("Game/AssetRegistry.bin"), temp.Path, _ => new ThrowingPayload());

        Assert.True(result.Failed);
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public void TryExportJson_FailsOnAnUnpairedSurrogateRatherThanRewritingIt()
    {
        using var temp = new TempDir();

        var result = SimpleExportSupport.TryExportJson(Item("Game/Localization/Game.locres"), temp.Path, _ => new SurrogatePayload());

        Assert.True(result.Failed);
    }

    private static ExportItemInfo Item(string path) =>
        new(new FakeVfsProvider(StringComparer.OrdinalIgnoreCase), new FakeGameFile(path, [0]), "");

    private sealed class NumericPayload
    {
        public float Single => 1.5f;
        public double Double => -12345.6789d;
        public double Large => 1.0e21d;
    }

    private sealed class ThrowingPayload
    {
        public string Written => "written";
        public string Broken => throw new InvalidOperationException("broken");
    }

    private sealed class SurrogatePayload
    {
        public string Text => "\uD800";
    }
}
