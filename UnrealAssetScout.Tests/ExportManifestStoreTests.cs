using UnrealAssetScout.Incremental;

namespace UnrealAssetScout.Tests;

public sealed class ExportManifestStoreTests
{
    [Fact]
    public void SaveThenLoad_RoundTripsEveryField()
    {
        using var dir = new TempDir();
        var manifest = new ExportManifest
        {
            Mode = "textures",
            Game = "GAME_UE5_1",
            Tool = [new ToolVersionPair("0.2.1.0+1a2a277", "1.0.0.0+a098f0b6")],
            SkipTypes = ["UTexture2D"],
            ScriptBytecode = true,
            Containers = ["Pal-Windows.pak"],
            Usmap = new ManifestUsmapBlock
            {
                Types = new Dictionary<int, string> { [0] = "9f2a" },
                Enums = new Dictionary<int, string> { [0] = "77ab" }
            },
            Paths = ["Pal/Content/T_Foo.uasset", "Pal/Content/T_Foo.uexp"],
            Outputs = ["Pal\\Content\\T_Foo.png"],
            UeTypes = ["Texture2D"],
            UeEnums = ["EPixelFormat"],
            ClrTypes = ["UTexture2D"],
            TypeSets = [[0]],
            ClrTypeSets = [[0]],
            Fingerprints = new Dictionary<int, string> { [0] = "3q2+7w==", [1] = "kZ8xAA==" },
            Sources = new Dictionary<int, ManifestSource>
            {
                [0] = new()
                {
                    C = [0, 1], D = [], O = [0], T = 0, U = null, X = 0,
                    E = false, B = BytecodeState.False, S = SourceStatus.Ok, Ms = 12.5
                }
            }
        };

        ExportManifestStore.Save(dir.Path, manifest);
        var loaded = ExportManifestStore.TryLoad(dir.Path, out var error);

        Assert.Null(error);
        Assert.NotNull(loaded);
        Assert.Equal(ExportManifestStore.CurrentSchema, loaded.Schema);
        Assert.Equal("textures", loaded.Mode);
        Assert.Equal("GAME_UE5_1", loaded.Game);
        Assert.Equal("1.0.0.0+a098f0b6", Assert.Single(loaded.Tool).Cue4Parse);
        Assert.Equal(["UTexture2D"], loaded.SkipTypes);
        Assert.True(loaded.ScriptBytecode);
        Assert.Equal(["Pal-Windows.pak"], loaded.Containers);
        Assert.Equal("9f2a", loaded.Usmap.Types[0]);
        Assert.Equal("77ab", loaded.Usmap.Enums[0]);
        Assert.Equal(2, loaded.Paths.Count);
        Assert.Equal("Pal\\Content\\T_Foo.png", Assert.Single(loaded.Outputs));
        Assert.Equal([0], Assert.Single(loaded.TypeSets));
        Assert.Equal("kZ8xAA==", loaded.Fingerprints[1]);

        var source = loaded.Sources[0];
        Assert.Equal([0, 1], source.C);
        Assert.Empty(source.D);
        Assert.Equal([0], source.O);
        Assert.Equal(0, source.T);
        Assert.Null(source.U);
        Assert.Equal(0, source.X);
        Assert.False(source.E);
        Assert.Equal("false", source.B);
        Assert.Equal("ok", source.S);
        Assert.Equal(12.5, source.Ms);
    }

    [Fact]
    public void Save_KeepsEachSourceEntryOnOneLine()
    {
        // Indenting the entries too would bury the global block and roughly double the file.
        using var dir = new TempDir();
        var manifest = new ExportManifest { Mode = "json", Game = "GAME_UE5_1" };
        manifest.Paths.Add("Game/A.uasset");
        manifest.Sources[0] = new ManifestSource
        {
            C = [0], O = [], B = BytecodeState.False, S = SourceStatus.Ok, Ms = 1.5
        };
        ExportManifestStore.Save(dir.Path, manifest);

        var entryLine = Assert.Single(
            File.ReadAllLines(ExportManifestStore.PathFor(dir.Path)).Where(line => line.Contains("\"c\"")));

        Assert.Contains("\"ms\":1.5", entryLine);
        Assert.EndsWith("}", entryLine.TrimEnd(','));
    }

    [Fact]
    public void Save_WritesIndentedJsonSoTheGlobalBlockCanBeRead()
    {
        using var dir = new TempDir();
        ExportManifestStore.Save(dir.Path, new ExportManifest { Mode = "json", Game = "GAME_UE5_1" });

        var text = File.ReadAllText(ExportManifestStore.PathFor(dir.Path));

        Assert.Contains(Environment.NewLine, text);
        Assert.Contains("  \"mode\": \"json\"", text);
    }

    [Fact]
    public void Save_UsesShortJsonKeys()
    {
        using var dir = new TempDir();
        var manifest = new ExportManifest
        {
            Sources = new Dictionary<int, ManifestSource>
            {
                [7] = new() { C = [1], O = [2], B = BytecodeState.Unknown, S = SourceStatus.Ok }
            }
        };

        ExportManifestStore.Save(dir.Path, manifest);
        var json = File.ReadAllText(ExportManifestStore.PathFor(dir.Path));

        Assert.Contains("\"sources\"", json);
        Assert.Contains("\"c\":", json);
        Assert.Contains("\"o\":", json);
        Assert.Contains("\"b\":", json);
        Assert.DoesNotContain("\"Constituents\"", json);
    }

    [Fact]
    public void TryLoad_NoManifest_ReturnsNullWithoutError()
    {
        using var dir = new TempDir();

        var loaded = ExportManifestStore.TryLoad(dir.Path, out var error);

        Assert.Null(loaded);
        Assert.Null(error);
    }

    [Fact]
    public void TryLoad_Truncated_ReturnsErrorNotNullManifest()
    {
        using var dir = new TempDir();
        File.WriteAllText(ExportManifestStore.PathFor(dir.Path), "{\"schema\": 1, \"mode\": \"json");

        var loaded = ExportManifestStore.TryLoad(dir.Path, out var error);

        Assert.Null(loaded);
        Assert.NotNull(error);
        Assert.Contains(ExportManifestStore.FileName, error);
    }

    [Fact]
    public void TryLoad_MatchingSchema_Succeeds()
    {
        using var dir = new TempDir();
        ExportManifestStore.Save(dir.Path, new ExportManifest { Schema = ExportManifestStore.CurrentSchema, Mode = "json" });

        var loaded = ExportManifestStore.TryLoad(dir.Path, out var error);

        Assert.Null(error);
        Assert.NotNull(loaded);
        Assert.Equal("json", loaded.Mode);
    }

    [Fact]
    public void TryLoad_DifferingSchema_ReturnsErrorNamingFoundAndExpectedAndMentionsRebuild()
    {
        // System.Text.Json silently ignores unknown properties and defaults missing ones, so an
        // unvalidated schema mismatch would load as a partially-defaulted manifest of the current
        // shape instead of failing, with the damage landing on the staleness rules.
        using var dir = new TempDir();
        File.WriteAllText(ExportManifestStore.PathFor(dir.Path), "{\"schema\": 1}");

        var loaded = ExportManifestStore.TryLoad(dir.Path, out var error);

        Assert.Null(loaded);
        Assert.NotNull(error);
        Assert.Contains("schema 1", error);
        Assert.Contains(ExportManifestStore.CurrentSchema.ToString(), error);
        Assert.Contains("--rebuild", error);
    }

    [Fact]
    public void Save_LeavesNoTempFileBehind()
    {
        using var dir = new TempDir();

        ExportManifestStore.Save(dir.Path, new ExportManifest { Mode = "json" });

        Assert.Equal([ExportManifestStore.FileName], Directory.GetFiles(dir.Path).Select(Path.GetFileName));
    }

    [Fact]
    public void Save_OverExistingManifest_Replaces()
    {
        using var dir = new TempDir();
        ExportManifestStore.Save(dir.Path, new ExportManifest { Mode = "json" });

        ExportManifestStore.Save(dir.Path, new ExportManifest { Mode = "textures" });

        Assert.Equal("textures", ExportManifestStore.TryLoad(dir.Path, out _)!.Mode);
    }

    [Fact]
    public void Save_ConsumesStaleTempFileViaMove()
    {
        using var dir = new TempDir();
        var tempPath = ExportManifestStore.PathFor(dir.Path) + ".tmp";
        File.WriteAllText(tempPath, "stale");

        ExportManifestStore.Save(dir.Path, new ExportManifest { Mode = "json" });

        Assert.False(File.Exists(tempPath));
    }
}
