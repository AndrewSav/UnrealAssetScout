using UnrealAssetScout.Incremental;

namespace UnrealAssetScout.Tests;

public sealed class ManifestBuilderTests
{
    private static ManifestBuilder NewBuilder() => new(
        mode: "json",
        game: "GAME_UE5_1",
        tool: [new ToolVersionPair(1, "b")],
        skipTypes: [],
        scriptBytecode: false,
        containers: ["a.pak"]);

    [Fact]
    public void AddRecorded_InternsPathsOnceAndReferencesThemById()
    {
        var builder = NewBuilder();

        builder.AddRecorded(new SourceRecord
        {
            Path = "Game/A.uasset",
            Constituents = ["Game/A.uasset", "Game/A.uexp"],
            Dependencies = ["Game/B.uasset"],
            Outputs = ["Game\\A.json"],
            ClrTypes = ["UTexture2D"],
            Bytecode = BytecodeState.False,
            Status = SourceStatus.Ok
        });
        builder.AddRecorded(new SourceRecord
        {
            Path = "Game/B.uasset",
            Constituents = ["Game/B.uasset"],
            Dependencies = ["Game/A.uasset"],
            Outputs = ["Game\\B.json"],
            ClrTypes = ["UTexture2D"],
            Bytecode = BytecodeState.False,
            Status = SourceStatus.Ok
        });

        var manifest = builder.Build();

        // Game/A.uasset, Game/A.uexp, Game/B.uasset - each exactly once.
        Assert.Equal(3, manifest.Paths.Count);
        Assert.Equal(3, manifest.Paths.Distinct().Count());
        Assert.Single(manifest.ClrTypes);

        var a = manifest.Sources[manifest.Paths.IndexOf("Game/A.uasset")];
        Assert.Equal(manifest.Paths.IndexOf("Game/B.uasset"), Assert.Single(a.D));
    }

    [Fact]
    public void AddRecorded_InternsLayoutProvidersIntoThePathsTable()
    {
        var builder = NewBuilder();

        builder.AddRecorded(new SourceRecord
        {
            Path = "Game/A.uasset",
            Constituents = ["Game/A.uasset"],
            LayoutProviders = ["Game/Base"],
            Milliseconds = 42.5,
            Outputs = ["Game\\A.json"],
            Bytecode = BytecodeState.False,
            Status = SourceStatus.Ok
        });

        var manifest = builder.Build();

        var entry = Assert.Single(manifest.Sources).Value;
        Assert.Equal("Game/Base", manifest.Paths[Assert.Single(entry.P)]);
        Assert.Equal(42.5, entry.Ms);
    }

    [Fact]
    public void AddRecorded_DeduplicatesIdenticalTypeSets()
    {
        var builder = NewBuilder();

        foreach (var name in new[] { "A", "B", "C" })
        {
            builder.AddRecorded(new SourceRecord
            {
                Path = $"Game/{name}.uasset",
                Constituents = [$"Game/{name}.uasset"],
                UsmapTypes = ["Texture2D", "Object"],
                Bytecode = BytecodeState.Unknown,
                Status = SourceStatus.Ok
            });
        }

        var manifest = builder.Build();

        Assert.Single(manifest.TypeSets);
        Assert.All(manifest.Sources.Values, source => Assert.Equal(0, source.T));
    }

    [Fact]
    public void AddRecorded_SetMembershipIsOrderIndependent()
    {
        var builder = NewBuilder();

        builder.AddRecorded(new SourceRecord
        {
            Path = "Game/A.uasset", Constituents = ["Game/A.uasset"],
            UsmapTypes = ["Object", "Texture2D"], Bytecode = BytecodeState.Unknown, Status = SourceStatus.Ok
        });
        builder.AddRecorded(new SourceRecord
        {
            Path = "Game/B.uasset", Constituents = ["Game/B.uasset"],
            UsmapTypes = ["Texture2D", "Object"], Bytecode = BytecodeState.Unknown, Status = SourceStatus.Ok
        });

        Assert.Single(builder.Build().TypeSets);
    }

    [Fact]
    public void AddRecorded_NullTypeSetStaysNull()
    {
        var builder = NewBuilder();

        builder.AddRecorded(new SourceRecord
        {
            Path = "Game/A.uasset", Constituents = ["Game/A.uasset"],
            UsmapTypes = null, UnknownTypes = null, ClrTypes = null,
            Bytecode = BytecodeState.Unknown, Status = SourceStatus.Ok
        });

        var source = Assert.Single(builder.Build().Sources).Value;
        Assert.Null(source.T);
        Assert.Null(source.U);
        Assert.Null(source.X);
    }

    [Fact]
    public void CarryForward_ReInternsIdsWhenOldAndNewTablesDiffer()
    {
        // Old tables deliberately hold extra leading entries so every id shifts.
        var old = new ExportManifest
        {
            Paths = ["Removed/Zero.uasset", "Removed/One.uexp", "Game/A.uasset", "Game/A.uexp", "Game/B.uasset", "Game/Base"],
            Outputs = ["Removed\\Zero.json", "Game\\A.json"],
            UeTypes = ["Gone", "Texture2D", "Object"],
            ClrTypes = ["UGone", "UTexture2D"],
            TypeSets = [[0], [1], [2]],
            ClrTypeSets = [[0], [1]],
            Sources = new Dictionary<int, ManifestSource>
            {
                [2] = new()
                {
                    C = [2, 3], D = [4], P = [5], O = [1], T = 1, U = 2, X = 1,
                    E = false, B = BytecodeState.True, S = SourceStatus.Ok, Ms = 7.25
                }
            }
        };
        var builder = NewBuilder();

        builder.CarryForward("Game/A.uasset", old, old.Sources[2]);
        var manifest = builder.Build();

        var carried = Assert.Single(manifest.Sources).Value;
        Assert.Equal(["Game/A.uasset", "Game/A.uexp", "Game/B.uasset", "Game/Base"], manifest.Paths);
        Assert.Equal([0, 1], carried.C);
        Assert.Equal([2], carried.D);
        Assert.Equal([3], carried.P);
        Assert.Equal(["Game\\A.json"], manifest.Outputs);
        Assert.Equal([0], carried.O);
        Assert.Equal(["Texture2D", "Object"], manifest.UeTypes);
        Assert.Equal([[0], [1]], manifest.TypeSets);
        Assert.Equal(0, carried.T);
        Assert.Equal(1, carried.U);
        Assert.Equal(["UTexture2D"], manifest.ClrTypes);
        Assert.Equal(0, carried.X);
        Assert.Equal(BytecodeState.True, carried.B);
        // Carried, so a plan can total the cost of sources this run did not re-export.
        Assert.Equal(7.25, carried.Ms);
    }

    // Old ids for UTexture2D/UTexture/UObject are 1/2/3; the fresh builder assigns 0/1/2. The chain
    // must be re-derived from names, the way CarryForward's other tables already are, not copied by
    // id: a raw id copy would still pass by coincidence if old and new ids happened to line up.
    // UTexture and UObject are base types only, never a leaf recorded through any source's X.
    [Fact]
    public void CarryForward_ReInternsClrTypeChainNamesThroughTheNewClrTypesTable()
    {
        var old = new ExportManifest
        {
            Paths = ["Game/A.uasset"],
            ClrTypes = ["UGone", "UTexture2D", "UTexture", "UObject"],
            ClrTypeSets = [[1]],
            ClrTypeChains = new Dictionary<int, List<int>> { [1] = [1, 2, 3] },
            Sources = new Dictionary<int, ManifestSource>
            {
                [0] = new() { C = [0], X = 0, B = BytecodeState.False, S = SourceStatus.Ok }
            }
        };
        var builder = NewBuilder();

        builder.CarryForward("Game/A.uasset", old, old.Sources[0]);
        var manifest = builder.Build();

        var leafId = manifest.ClrTypes.IndexOf("UTexture2D");
        var chainNames = manifest.ClrTypeChains[leafId].Select(id => manifest.ClrTypes[id]);
        Assert.Equal(["UTexture2D", "UTexture", "UObject"], chainNames);
    }

    [Fact]
    public void SetFingerprint_PersistsOnlyReferencedPaths()
    {
        var builder = NewBuilder();
        builder.AddRecorded(new SourceRecord
        {
            Path = "Game/A.uasset", Constituents = ["Game/A.uasset"], Dependencies = ["Game/B.uasset"],
            Bytecode = BytecodeState.Unknown, Status = SourceStatus.Ok
        });

        builder.SetFingerprint("Game/A.uasset", "aaaa");
        builder.SetFingerprint("Game/B.uasset", "bbbb");
        builder.SetFingerprint("Game/NeverReferenced.uasset", "cccc");

        var manifest = builder.Build();

        Assert.Equal(2, manifest.Fingerprints.Count);
        Assert.DoesNotContain("Game/NeverReferenced.uasset", manifest.Paths);
        Assert.Equal("aaaa", manifest.Fingerprints[manifest.Paths.IndexOf("Game/A.uasset")]);
        Assert.Equal("bbbb", manifest.Fingerprints[manifest.Paths.IndexOf("Game/B.uasset")]);
    }

    // UTexture and UObject appear only as base types in the chain, never as a leaf export type
    // recorded through ClrTypes. Build must intern them into ClrTypes while assembling
    // ClrTypeChains, and every id the chain holds must resolve back through the finished table.
    [Fact]
    public void Build_ChainNameNeverSeenAsALeaf_IsInternedAndChainResolvesById()
    {
        var builder = NewBuilder();

        builder.AddRecorded(new SourceRecord
        {
            Path = "Game/A.uasset",
            Constituents = ["Game/A.uasset"],
            ClrTypes = ["UTexture2D"],
            ClrTypeChains = new Dictionary<string, List<string>>
            {
                ["UTexture2D"] = ["UTexture2D", "UTexture", "UObject"]
            },
            Bytecode = BytecodeState.False,
            Status = SourceStatus.Ok
        });

        var manifest = builder.Build();

        Assert.Contains("UTexture", manifest.ClrTypes);
        Assert.Contains("UObject", manifest.ClrTypes);

        var leafId = manifest.ClrTypes.IndexOf("UTexture2D");
        var chainIds = manifest.ClrTypeChains[leafId];
        Assert.All(chainIds, id => Assert.InRange(id, 0, manifest.ClrTypes.Count - 1));
        Assert.Equal(["UTexture2D", "UTexture", "UObject"], chainIds.Select(id => manifest.ClrTypes[id]));
    }
}
