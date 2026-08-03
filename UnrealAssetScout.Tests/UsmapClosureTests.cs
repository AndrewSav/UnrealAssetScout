using UnrealAssetScout.Incremental;

namespace UnrealAssetScout.Tests;

public sealed class UsmapClosureTests
{
    private static UsmapSnapshot Snapshot(params UsmapTypeNode[] types) => new()
    {
        TypeFingerprints = types.ToDictionary(type => type.Name, type => "fp-" + type.Name),
        EnumFingerprints = new Dictionary<string, string>(),
        Types = types.ToDictionary(type => type.Name)
    };

    [Fact]
    public void Of_IncludesTheNameItself()
    {
        var closure = new UsmapClosure(Snapshot(new UsmapTypeNode("A", null, [], [])));

        Assert.Equal(["A"], closure.Of("A"));
    }

    [Fact]
    public void Of_FollowsSupertypes()
    {
        var closure = new UsmapClosure(Snapshot(
            new UsmapTypeNode("A", "B", [], []),
            new UsmapTypeNode("B", "C", [], []),
            new UsmapTypeNode("C", null, [], [])));

        Assert.Equal(["A", "B", "C"], closure.Of("A").Order());
    }

    [Fact]
    public void Of_FollowsNestedStructAndEnumReferences()
    {
        var closure = new UsmapClosure(Snapshot(
            new UsmapTypeNode("A", null, ["Inner"], ["EFlag"]),
            new UsmapTypeNode("Inner", null, ["Deeper"], []),
            new UsmapTypeNode("Deeper", null, [], ["EDeep"])));

        Assert.Equal(["A", "Deeper", "EDeep", "EFlag", "Inner"], closure.Of("A").Order());
    }

    [Fact]
    public void Of_SelfReferentialTypeTerminates()
    {
        // The usmap format is flat and cannot express namespaced same-named structs, so a valid
        // parent-child relationship can collapse into an apparent cycle. It must not hang.
        var closure = new UsmapClosure(Snapshot(
            new UsmapTypeNode("A", "A", ["A"], [])));

        Assert.Equal(["A"], closure.Of("A"));
    }

    [Fact]
    public void Of_MutualCycleTerminates()
    {
        var closure = new UsmapClosure(Snapshot(
            new UsmapTypeNode("A", null, ["B"], []),
            new UsmapTypeNode("B", null, ["A"], [])));

        Assert.Equal(["A", "B"], closure.Of("A").Order());
    }

    [Fact]
    public void Of_UnknownNameYieldsJustItself()
    {
        var closure = new UsmapClosure(Snapshot(new UsmapTypeNode("A", null, [], [])));

        Assert.Equal(["Missing"], closure.Of("Missing"));
    }

    [Fact]
    public void IntersectsAny_TrueOnlyWhenTheClosureTouchesAChangedName()
    {
        var closure = new UsmapClosure(Snapshot(
            new UsmapTypeNode("A", null, ["Inner"], []),
            new UsmapTypeNode("Inner", null, [], []),
            new UsmapTypeNode("Unrelated", null, [], [])));

        Assert.True(closure.IntersectsAny(["A"], new HashSet<string> { "Inner" }));
        Assert.False(closure.IntersectsAny(["A"], new HashSet<string> { "Unrelated" }));
    }
}
