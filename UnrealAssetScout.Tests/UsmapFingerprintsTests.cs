using CUE4Parse.MappingsProvider;
using UnrealAssetScout.Incremental;

namespace UnrealAssetScout.Tests;

public sealed class UsmapFingerprintsTests
{
    private static Struct Type(
        TypeMappings context, string name, string? super, params (int Index, string Name, PropertyType Type)[] properties)
    {
        var map = properties.ToDictionary(
            property => property.Index,
            property => new PropertyInfo(property.Index, property.Name, property.Type, 1));

        var result = new Struct(context, name, super, map, properties.Length);
        context.Types[name] = result;
        return result;
    }

    private static PropertyType Simple(string type) => new(type);

    [Fact]
    public void From_NullMappings_ReturnsEmptySnapshot()
    {
        var snapshot = UsmapFingerprints.From(null);

        Assert.Empty(snapshot.TypeFingerprints);
        Assert.Empty(snapshot.EnumFingerprints);
        Assert.Empty(snapshot.Types);
    }

    [Fact]
    public void From_SameSchemaDifferentInsertionOrder_ProducesEqualFingerprints()
    {
        var first = new TypeMappings();
        Type(first, "A", null, (0, "X", Simple("IntProperty")), (1, "Y", Simple("BoolProperty")));
        Type(first, "B", null, (0, "Z", Simple("IntProperty")));

        var second = new TypeMappings();
        Type(second, "B", null, (0, "Z", Simple("IntProperty")));
        Type(second, "A", null, (0, "X", Simple("IntProperty")), (1, "Y", Simple("BoolProperty")));

        Assert.Equal(
            UsmapFingerprints.From(first).TypeFingerprints["A"],
            UsmapFingerprints.From(second).TypeFingerprints["A"]);
    }

    [Fact]
    public void From_RenamedProperty_ChangesTheFingerprint()
    {
        var before = new TypeMappings();
        Type(before, "A", null, (0, "X", Simple("IntProperty")));
        var after = new TypeMappings();
        Type(after, "A", null, (0, "Renamed", Simple("IntProperty")));

        Assert.NotEqual(
            UsmapFingerprints.From(before).TypeFingerprints["A"],
            UsmapFingerprints.From(after).TypeFingerprints["A"]);
    }

    [Fact]
    public void From_ReorderedProperties_ChangesTheFingerprint()
    {
        var before = new TypeMappings();
        Type(before, "A", null, (0, "X", Simple("IntProperty")), (1, "Y", Simple("BoolProperty")));
        var after = new TypeMappings();
        Type(after, "A", null, (0, "Y", Simple("BoolProperty")), (1, "X", Simple("IntProperty")));

        Assert.NotEqual(
            UsmapFingerprints.From(before).TypeFingerprints["A"],
            UsmapFingerprints.From(after).TypeFingerprints["A"]);
    }

    [Fact]
    public void From_ChangedPropertyType_ChangesTheFingerprint()
    {
        var before = new TypeMappings();
        Type(before, "A", null, (0, "X", Simple("IntProperty")));
        var after = new TypeMappings();
        Type(after, "A", null, (0, "X", Simple("FloatProperty")));

        Assert.NotEqual(
            UsmapFingerprints.From(before).TypeFingerprints["A"],
            UsmapFingerprints.From(after).TypeFingerprints["A"]);
    }

    [Fact]
    public void From_ChangedSupertype_ChangesTheFingerprint()
    {
        var before = new TypeMappings();
        Type(before, "A", "Base", (0, "X", Simple("IntProperty")));
        var after = new TypeMappings();
        Type(after, "A", "OtherBase", (0, "X", Simple("IntProperty")));

        Assert.NotEqual(
            UsmapFingerprints.From(before).TypeFingerprints["A"],
            UsmapFingerprints.From(after).TypeFingerprints["A"]);
    }

    [Fact]
    public void From_ExtractsSupertypeAndReferencedStructAndEnumNames()
    {
        var mappings = new TypeMappings();
        Type(mappings, "A", "Base",
            (0, "Nested", new PropertyType("StructProperty", structType: "Inner")),
            (1, "Flag", new PropertyType("EnumProperty", enumName: "EFlag")));

        var node = UsmapFingerprints.From(mappings).Types["A"];

        Assert.Equal("Base", node.Super);
        Assert.Equal(["Inner"], node.ReferencedTypes);
        Assert.Equal(["EFlag"], node.ReferencedEnums);
    }

    [Fact]
    public void From_ExtractsReferencesNestedInsideArrayAndMapProperties()
    {
        var mappings = new TypeMappings();
        Type(mappings, "A", null,
            (0, "List", new PropertyType("ArrayProperty",
                innerType: new PropertyType("StructProperty", structType: "Element"))),
            (1, "Lookup", new PropertyType("MapProperty",
                innerType: new PropertyType("EnumProperty", enumName: "EKey"),
                valueType: new PropertyType("StructProperty", structType: "Value"))));

        var node = UsmapFingerprints.From(mappings).Types["A"];

        Assert.Equal(["Element", "Value"], node.ReferencedTypes.Order());
        Assert.Equal(["EKey"], node.ReferencedEnums);
    }

    [Fact]
    public void From_EnumMemberOrderDoesNotMatterButValuesDo()
    {
        var ordered = new TypeMappings();
        ordered.Enums["E"] = new Dictionary<long, string> { [0] = "A", [1] = "B" };
        var reordered = new TypeMappings();
        reordered.Enums["E"] = new Dictionary<long, string> { [1] = "B", [0] = "A" };
        var renumbered = new TypeMappings();
        renumbered.Enums["E"] = new Dictionary<long, string> { [0] = "A", [2] = "B" };

        Assert.Equal(
            UsmapFingerprints.From(ordered).EnumFingerprints["E"],
            UsmapFingerprints.From(reordered).EnumFingerprints["E"]);
        Assert.NotEqual(
            UsmapFingerprints.From(ordered).EnumFingerprints["E"],
            UsmapFingerprints.From(renumbered).EnumFingerprints["E"]);
    }

    [Fact]
    public void From_SelfReferentialTypeDoesNotRecurse()
    {
        // CUE4Parse's Struct.TryGetValue has no cycle guard and such entries occur in practice,
        // so the fingerprint must never walk the Super chain.
        var mappings = new TypeMappings();
        Type(mappings, "A", "A", (0, "Self", new PropertyType("StructProperty", structType: "A")));

        var snapshot = UsmapFingerprints.From(mappings);

        Assert.NotEmpty(snapshot.TypeFingerprints["A"]);
    }

    [Fact]
    public void From_PropagatesTheTypesComparerSoACaseDifferingReferenceResolves()
    {
        // CUE4Parse's own usmap and jmap parsers build Types with StringComparer.OrdinalIgnoreCase,
        // so a StructType or SuperType reference can legitimately differ in case from the key the
        // referenced struct was stored under. The produced snapshot must resolve such a reference
        // the same way, or the closure walk that consumes it stops silently at that node.
        var mappings = new TypeMappings(
            new Dictionary<string, Struct>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, Dictionary<long, string>>());
        Type(mappings, "Base", null, (0, "X", Simple("IntProperty")));
        Type(mappings, "A", null, (0, "Nested", new PropertyType("StructProperty", structType: "base")));

        var snapshot = UsmapFingerprints.From(mappings);

        Assert.True(snapshot.Types.TryGetValue("base", out var resolved));
        Assert.Equal("Base", resolved.Name);
    }

    [Fact]
    public void From_NonConsecutiveIndexGapChangesTheFingerprint()
    {
        // Real usmap indices are not guaranteed gap-free: a stripped property elsewhere in the
        // index space leaves a gap without touching the surviving properties' names or types. Both
        // fixtures below have the same two properties in the same relative order, so this only
        // distinguishes a fingerprint that embeds the index itself from one that uses the index
        // solely to sort properties before hashing.
        var before = new TypeMappings();
        Type(before, "A", null, (0, "X", Simple("IntProperty")), (1, "Y", Simple("BoolProperty")));
        var after = new TypeMappings();
        Type(after, "A", null, (0, "X", Simple("IntProperty")), (5, "Y", Simple("BoolProperty")));

        Assert.NotEqual(
            UsmapFingerprints.From(before).TypeFingerprints["A"],
            UsmapFingerprints.From(after).TypeFingerprints["A"]);
    }
}
