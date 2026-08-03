using CUE4Parse.MappingsProvider;
using UnrealAssetScout.Incremental;

namespace UnrealAssetScout.Tests;

public sealed class ExportPlannerUsmapTests
{
    private static UsmapSnapshot Snapshot(
        Dictionary<string, string>? typeFingerprints = null,
        Dictionary<string, string>? enumFingerprints = null,
        params UsmapTypeNode[] types) => new()
    {
        TypeFingerprints = typeFingerprints ?? types.ToDictionary(type => type.Name, type => "fp-" + type.Name),
        EnumFingerprints = enumFingerprints ?? new Dictionary<string, string>(),
        Types = types.ToDictionary(type => type.Name)
    };

    // A manifest whose single source recorded usmap type "Texture2D", with the usmap block
    // holding fingerprints for Texture2D and its nested struct Inner.
    private static ExportManifest ManifestWithUsmap(string texture2DFingerprint, string innerFingerprint)
    {
        var manifest = PlanInputsFixture.Manifest("Game/A.uasset");
        manifest.UeTypes = ["Texture2D", "Inner"];
        manifest.TypeSets = [[0]];
        manifest.Sources[0].T = 0;
        manifest.Usmap.Types = new Dictionary<int, string>
        {
            [0] = texture2DFingerprint,
            [1] = innerFingerprint
        };
        return manifest;
    }

    private static UsmapTypeNode[] Graph() =>
    [
        new UsmapTypeNode("Texture2D", null, ["Inner"], []),
        new UsmapTypeNode("Inner", null, [], []),
        new UsmapTypeNode("Unrelated", null, [], [])
    ];

    [Fact]
    public void Plan_UsmapUnchanged_DoesNotInvalidate()
    {
        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: ManifestWithUsmap("fp-Texture2D", "fp-Inner"),
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset"),
            usmap: Snapshot(types: Graph())));

        Assert.Empty(result.Plan!.WorkList);
    }

    [Fact]
    public void Plan_ChangedTypeInsideTheClosure_IsStale()
    {
        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: ManifestWithUsmap("fp-Texture2D", "fp-Inner"),
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset"),
            usmap: Snapshot(
                typeFingerprints: new Dictionary<string, string>
                {
                    ["Texture2D"] = "CHANGED", ["Inner"] = "fp-Inner", ["Unrelated"] = "fp-Unrelated"
                },
                types: Graph())));

        Assert.Equal(["Game/A.uasset"], result.Plan!.WorkList);
    }

    [Fact]
    public void Plan_ChangeReachedOnlyThroughANestedStruct_IsStale()
    {
        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: ManifestWithUsmap("fp-Texture2D", "fp-Inner"),
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset"),
            usmap: Snapshot(
                typeFingerprints: new Dictionary<string, string>
                {
                    ["Texture2D"] = "fp-Texture2D", ["Inner"] = "CHANGED", ["Unrelated"] = "fp-Unrelated"
                },
                types: Graph())));

        Assert.Equal(["Game/A.uasset"], result.Plan!.WorkList);
    }

    [Fact]
    public void Plan_ChangedTypeOutsideTheClosure_DoesNotInvalidate()
    {
        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: ManifestWithUsmap("fp-Texture2D", "fp-Inner"),
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset"),
            usmap: Snapshot(
                typeFingerprints: new Dictionary<string, string>
                {
                    ["Texture2D"] = "fp-Texture2D", ["Inner"] = "fp-Inner", ["Unrelated"] = "CHANGED"
                },
                types: Graph())));

        Assert.Empty(result.Plan!.WorkList);
    }

    [Fact]
    public void Plan_TypeRemovedFromUsmap_IsAPresenceChangeAndIsStale()
    {
        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: ManifestWithUsmap("fp-Texture2D", "fp-Inner"),
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset"),
            usmap: Snapshot(
                typeFingerprints: new Dictionary<string, string> { ["Inner"] = "fp-Inner" },
                types: [new UsmapTypeNode("Inner", null, [], [])])));

        Assert.Equal(["Game/A.uasset"], result.Plan!.WorkList);
    }

    // Texture2D was recorded alone; the usmap has since grown a new struct, Inner, that
    // Texture2D's properties now reference. Inner has no recorded fingerprint at all, so this is
    // a pure appearance reachable through the closure, not a value change on an already-known name.
    [Fact]
    public void Plan_TypeAppearedInUsmapAndReachableFromARecordedType_IsStale()
    {
        var manifest = PlanInputsFixture.Manifest("Game/A.uasset");
        manifest.UeTypes = ["Texture2D"];
        manifest.TypeSets = [[0]];
        manifest.Sources[0].T = 0;
        manifest.Usmap.Types = new Dictionary<int, string> { [0] = "fp-Texture2D" };

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset"),
            usmap: Snapshot(
                typeFingerprints: new Dictionary<string, string>
                {
                    ["Texture2D"] = "fp-Texture2D", ["Inner"] = "fp-Inner"
                },
                types:
                [
                    new UsmapTypeNode("Texture2D", null, ["Inner"], []),
                    new UsmapTypeNode("Inner", null, [], [])
                ])));

        Assert.Equal(["Game/A.uasset"], result.Plan!.WorkList);
    }

    [Fact]
    public void Plan_ChangedEnumInsideTheClosure_IsStale()
    {
        var manifest = ManifestWithUsmap("fp-Texture2D", "fp-Inner");
        manifest.UeEnums = ["EPixelFormat"];
        manifest.Usmap.Enums = new Dictionary<int, string> { [0] = "fp-EPixelFormat" };

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset"),
            usmap: Snapshot(
                enumFingerprints: new Dictionary<string, string> { ["EPixelFormat"] = "CHANGED" },
                types:
                [
                    new UsmapTypeNode("Texture2D", null, ["Inner"], ["EPixelFormat"]),
                    new UsmapTypeNode("Inner", null, [], [])
                ])));

        Assert.Equal(["Game/A.uasset"], result.Plan!.WorkList);
    }

    [Fact]
    public void Plan_RecordedStopNameNowPresentInUsmap_IsStale()
    {
        var manifest = PlanInputsFixture.Manifest("Game/A.uasset");
        manifest.UeTypes = ["BP_Thing_C"];
        manifest.TypeSets = [[0]];
        manifest.Sources[0].T = null;
        manifest.Sources[0].U = 0;

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset"),
            usmap: Snapshot(types: [new UsmapTypeNode("BP_Thing_C", null, [], [])])));

        Assert.Equal(["Game/A.uasset"], result.Plan!.WorkList);
    }

    [Fact]
    public void Plan_RecordedStopEnumNameNowPresentInUsmap_IsStale()
    {
        var manifest = PlanInputsFixture.Manifest("Game/A.uasset");
        manifest.UeTypes = ["EBlockedChoice"];
        manifest.TypeSets = [[0]];
        manifest.Sources[0].T = null;
        manifest.Sources[0].U = 0;

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset"),
            usmap: Snapshot(enumFingerprints: new Dictionary<string, string>
            {
                ["EBlockedChoice"] = "fp-EBlockedChoice"
            })));

        Assert.Equal(["Game/A.uasset"], result.Plan!.WorkList);
    }

    [Fact]
    public void Plan_RecordedStopNameStillAbsent_DoesNotInvalidate()
    {
        var manifest = PlanInputsFixture.Manifest("Game/A.uasset");
        manifest.UeTypes = ["BP_Thing_C"];
        manifest.TypeSets = [[0]];
        manifest.Sources[0].T = null;
        manifest.Sources[0].U = 0;

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset"),
            usmap: Snapshot(types: [new UsmapTypeNode("Other", null, [], [])])));

        Assert.Empty(result.Plan!.WorkList);
    }

    // Builds a TypeMappings the way CUE4Parse's own usmap and jmap parsers do: Types under
    // OrdinalIgnoreCase, Enums under the default (case-sensitive) comparer. Texture2D always
    // references Inner as "inner", a differing case from the key Inner is stored under, so the
    // snapshot this produces exercises the real lookup path rather than the hand-built Snapshot
    // helper above, which never reproduces CUE4Parse's comparer choice.
    private static TypeMappings MappingsReferencingInnerByLowercaseName(int innerPropertyCount)
    {
        var mappings = new TypeMappings(
            new Dictionary<string, Struct>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, Dictionary<long, string>>());

        var innerProperties = Enumerable.Range(0, innerPropertyCount).ToDictionary(
            index => index, index => new PropertyInfo(index, $"Field{index}", new PropertyType("IntProperty"), 1));
        mappings.Types["Inner"] = new Struct(mappings, "Inner", null, innerProperties, innerPropertyCount);

        var texture2DProperties = new Dictionary<int, PropertyInfo>
        {
            [0] = new PropertyInfo(0, "Nested", new PropertyType("StructProperty", structType: "inner"), 1)
        };
        mappings.Types["Texture2D"] = new Struct(mappings, "Texture2D", null, texture2DProperties, 1);

        return mappings;
    }

    [Fact]
    public void Plan_ChangeReachedThroughACaseDifferingStructReference_IsStale()
    {
        var before = UsmapFingerprints.From(MappingsReferencingInnerByLowercaseName(innerPropertyCount: 1));
        var after = UsmapFingerprints.From(MappingsReferencingInnerByLowercaseName(innerPropertyCount: 2));

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: ManifestWithUsmap(before.TypeFingerprints["Texture2D"], before.TypeFingerprints["Inner"]),
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset"),
            usmap: after));

        Assert.Equal(["Game/A.uasset"], result.Plan!.WorkList);
    }

    // Enum references have no resolution step in UsmapClosure at all (unlike structs, which at
    // least get a snapshot.Types.TryGetValue attempt), so the raw, differently cased reference
    // string is the only thing that ever reaches the visited set. This is the unconditional
    // exposure: no comparer propagated into UsmapFingerprints could fix it, only the comparer on
    // the changed-name set that visited is compared against.
    private static TypeMappings MappingsReferencingEnumByLowercaseName(Dictionary<long, string> enumMembers)
    {
        var mappings = new TypeMappings(
            new Dictionary<string, Struct>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, Dictionary<long, string>>());

        mappings.Enums["EPixelFormat"] = enumMembers;

        var texture2DProperties = new Dictionary<int, PropertyInfo>
        {
            [0] = new PropertyInfo(0, "Format", new PropertyType("EnumProperty", enumName: "epixelformat"), 1)
        };
        mappings.Types["Texture2D"] = new Struct(mappings, "Texture2D", null, texture2DProperties, 1);

        return mappings;
    }

    [Fact]
    public void Plan_ChangeReachedThroughACaseDifferingEnumReference_IsStale()
    {
        var before = UsmapFingerprints.From(MappingsReferencingEnumByLowercaseName(
            new Dictionary<long, string> { [0] = "PF_Unknown" }));
        var after = UsmapFingerprints.From(MappingsReferencingEnumByLowercaseName(
            new Dictionary<long, string> { [0] = "PF_Unknown", [1] = "PF_A8" }));

        var manifest = PlanInputsFixture.Manifest("Game/A.uasset");
        manifest.UeTypes = ["Texture2D"];
        manifest.TypeSets = [[0]];
        manifest.Sources[0].T = 0;
        manifest.UeEnums = ["EPixelFormat"];
        manifest.Usmap.Types = new Dictionary<int, string> { [0] = before.TypeFingerprints["Texture2D"] };
        manifest.Usmap.Enums = new Dictionary<int, string> { [0] = before.EnumFingerprints["EPixelFormat"] };

        var result = ExportPlanner.Plan(PlanInputsFixture.Create(
            manifest: manifest,
            sources: PlanInputsFixture.Sources("Game/A.uasset"),
            fingerprints: PlanInputsFixture.Fingerprints("Game/A.uasset"),
            usmap: after));

        Assert.Equal(["Game/A.uasset"], result.Plan!.WorkList);
    }
}
