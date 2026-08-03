using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Objects.UObject;
using UnrealAssetScout.Incremental;

namespace UnrealAssetScout.Tests;

public sealed class TypeChainWalkerTests
{
    [Fact]
    public void Classify_KnownNamesGoToKnown()
    {
        var result = TypeChainWalker.Classify(
            consulted: ["Texture2D", "Object"], usmap: Usmap("Texture2D", "Object"));

        Assert.Equal(["Object", "Texture2D"], result.Known.Order());
        Assert.Empty(result.Stopped);
    }

    [Fact]
    public void Classify_UnknownNamesGoToStopped()
    {
        // Every consulted name is one the deserializer asked the usmap for, so a name the usmap
        // could not answer is kept conservatively: its later appearance changes output.
        var result = TypeChainWalker.Classify(
            consulted: ["Texture2D", "BP_Thing_C"], usmap: Usmap("Texture2D"));

        Assert.Equal(["Texture2D"], result.Known);
        Assert.Equal(["BP_Thing_C"], result.Stopped);
    }

    [Fact]
    public void Classify_KnownEnumNamesGoToKnown()
    {
        var result = TypeChainWalker.Classify(
            consulted: ["EColorChannel"], usmap: UsmapOf([], ["EColorChannel"]));

        Assert.Equal(["EColorChannel"], result.Known);
        Assert.Empty(result.Stopped);
    }

    [Fact]
    public void Classify_DeduplicatesAcrossExports()
    {
        var result = TypeChainWalker.Classify(
            consulted: ["Object", "Object", "Texture2D", "X", "X"], usmap: Usmap("Object", "Texture2D"));

        Assert.Equal(2, result.Known.Count);
        Assert.Single(result.Stopped);
    }

    [Fact]
    public void Classify_EmptyInputProducesEmptyLists()
    {
        var result = TypeChainWalker.Classify(consulted: [], usmap: Usmap());

        Assert.Empty(result.Known);
        Assert.Empty(result.Stopped);
    }

    [Fact]
    public void Classify_KeepsOrdinallyDistinctNamesThatCultureCollationTreatsAsEqual()
    {
        // Precomposed "e-acute" (one UTF-16 code unit) and "e" followed by a combining acute
        // accent (two code units) are ordinally distinct, but the default, culture-sensitive
        // string comparer treats them as equal under every culture, including invariant, because
        // that equivalence is baked into Unicode collation rather than tied to one locale's rules.
        // A SortedSet built with that default comparer silently collapses the two into one entry,
        // dropping a real usmap type name out of Known. Ordinal must not do that.
        var precomposed = "CaféActor";
        var decomposed = "CaféActor";

        var result = TypeChainWalker.Classify(
            consulted: [precomposed, decomposed], usmap: Usmap(precomposed, decomposed));

        Assert.Equal(2, result.Known.Count);
        Assert.Contains(precomposed, result.Known);
        Assert.Contains(decomposed, result.Known);
    }

    [Fact]
    public void Walk_RecordsTheScriptClassWhereTheLiveChainEnds()
    {
        var result = TypeChainWalker.Walk(
            [ExportOf(new UScriptClass("DataTable"))], Usmap("DataTable"));

        Assert.Equal(["DataTable"], result.Known);
        Assert.Empty(result.Stopped);
    }

    [Fact]
    public void Walk_DoesNotRecordLiveChainLevels()
    {
        // The live levels take their layout from their own packages, so even though the usmap
        // also carries their names, only the script boundary is a consultation.
        var package = new StubPackage();
        var parent = new UStruct { Name = "WBP_Base_C", SuperStruct = package.IndexOf(new UScriptClass("UserWidget")) };
        var child = new UStruct { Name = "WBP_Button_C", SuperStruct = package.IndexOf(parent) };

        var result = TypeChainWalker.Walk(
            [ExportOf(child)], Usmap("WBP_Button_C", "WBP_Base_C", "UserWidget"));

        Assert.Equal(["UserWidget"], result.Known);
        Assert.Empty(result.Stopped);
    }

    [Fact]
    public void Walk_DeadSuperRecordsTheEndingLiveLevelNotTheDeadName()
    {
        // ConstructObject's chain repair reads the ending level's own usmap entry; nothing ever
        // looks up the unreachable super's name.
        var package = new StubPackage();
        var child = new UStruct { Name = "BP_Child_C", SuperStruct = package.UnloadableIndexOf("BP_Parent_C") };

        var known = TypeChainWalker.Walk([ExportOf(child)], Usmap("BP_Child_C", "BP_Parent_C"));
        Assert.Equal(["BP_Child_C"], known.Known);
        Assert.Empty(known.Stopped);

        var blocked = TypeChainWalker.Walk([ExportOf(child)], Usmap());
        Assert.Empty(blocked.Known);
        Assert.Equal(["BP_Child_C"], blocked.Stopped);
    }

    [Fact]
    public void Walk_RecordsPropertyReferencesOnlyWhenTheyDidNotResolveLive()
    {
        var package = new StubPackage();
        var widgetClass = new UStruct
        {
            Name = "WBP_Button_C",
            SuperStruct = package.IndexOf(new UScriptClass("UserWidget")),
            ChildProperties =
            [
                new FStructProperty { Struct = package.UnloadableIndexOf("Vector") },
                new FStructProperty { Struct = package.IndexOf(new UScriptClass("Guid")) },
                new FStructProperty { Struct = package.IndexOf(new UStruct { Name = "SLiveStruct" }) },
                new FEnumProperty { Enum = package.UnloadableIndexOf("ECoreState") },
                new FEnumProperty { Enum = package.IndexOf(new UEnum { Name = "ELiveChoice" }) },
                new FByteProperty { Enum = package.UnloadableIndexOf("EByteChoice") },
                new FArrayProperty { Inner = new FStructProperty { Struct = package.UnloadableIndexOf("LinearColor") } }
            ]
        };

        var result = TypeChainWalker.Walk(
            [ExportOf(widgetClass)],
            UsmapOf(
                ["Vector", "Guid", "SLiveStruct", "LinearColor", "UserWidget", "WBP_Button_C"],
                ["ECoreState", "ELiveChoice", "EByteChoice"]));

        Assert.Equal(
            ["EByteChoice", "ECoreState", "Guid", "LinearColor", "UserWidget", "Vector"],
            result.Known);
        Assert.Empty(result.Stopped);
    }

    [Fact]
    public void Walk_ReportsLiveChainLevelOwnersAsLayoutProviders()
    {
        var package = new StubPackage();
        var basePackage = new StubPackage { Name = "Game/UI/WBP_Base" };
        var parent = new UStruct { Name = "WBP_Base_C", SuperStruct = package.IndexOf(new UScriptClass("UserWidget")) };
        parent.Outer = new UnloadableObject(basePackage, "WBP_Base_C");
        var child = new UStruct { Name = "WBP_Button_C", SuperStruct = package.IndexOf(parent) };

        var layoutProviders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        TypeChainWalker.Walk([ExportOf(child)], Usmap(), layoutProviders);

        // The script boundary has no owning package, and the child struct here has no outer, so
        // the parent's package is the only provider the walk can see.
        Assert.Equal(["Game/UI/WBP_Base"], layoutProviders.ToList());
    }

    [Fact]
    public void Walk_ReportsLivePropertyReferenceOwnersAsLayoutProviders()
    {
        var package = new StubPackage();
        var structPackage = new StubPackage { Name = "Game/Structs/UserStruct" };
        var liveStruct = new UStruct { Name = "SUserStruct" };
        liveStruct.Outer = new UnloadableObject(structPackage, "SUserStruct");
        var enumPackage = new StubPackage { Name = "Game/Enums/UserEnum" };
        var liveEnum = new UEnum { Name = "EUserChoice" };
        liveEnum.Outer = new UnloadableObject(enumPackage, "EUserChoice");

        var widgetClass = new UStruct
        {
            Name = "WBP_X_C",
            ChildProperties =
            [
                new FStructProperty { Struct = package.IndexOf(liveStruct) },
                new FEnumProperty { Enum = package.IndexOf(liveEnum) },
                new FStructProperty { Struct = package.UnloadableIndexOf("Vector") }
            ]
        };

        var layoutProviders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        TypeChainWalker.Walk([ExportOf(widgetClass)], Usmap(), layoutProviders);

        // The usmap-consulted reference contributes no provider; the two live ones contribute
        // their owning packages.
        Assert.Equal(
            ["Game/Enums/UserEnum", "Game/Structs/UserStruct"],
            layoutProviders.Order(StringComparer.Ordinal).ToList());
    }

    [Fact]
    public void Walk_SelfReferentialChainTerminates()
    {
        var package = new StubPackage();
        var recursive = new UStruct { Name = "Recursive" };
        recursive.SuperStruct = package.IndexOf(recursive);

        var result = TypeChainWalker.Walk([ExportOf(recursive)], Usmap("Recursive"));

        Assert.Empty(result.Known);
        Assert.Empty(result.Stopped);
    }

    private static UsmapSnapshot Usmap(params string[] knownTypes) => UsmapOf(knownTypes, []);

    private static UsmapSnapshot UsmapOf(string[] knownTypes, string[] knownEnums) => new()
    {
        TypeFingerprints = knownTypes.ToDictionary(name => name, name => "fp-" + name),
        EnumFingerprints = knownEnums.ToDictionary(name => name, name => "fp-" + name),
        Types = knownTypes.ToDictionary(name => name, name => new UsmapTypeNode(name, null, [], []))
    };

    private static UObject ExportOf(UStruct exportClass) => new() { Class = new ResolvedLoadedObject(exportClass) };

    // Minimal IPackage that resolves hand-registered indices, so class chains and property
    // references can be built without real archives.
    private sealed class StubPackage : IPackage
    {
        private readonly List<ResolvedObject?> _resolutions = [null];

        public FPackageIndex IndexOf(UObject target) => Register(new ResolvedLoadedObject(target));

        public FPackageIndex UnloadableIndexOf(string name) => Register(new UnloadableObject(this, name));

        private FPackageIndex Register(ResolvedObject resolution)
        {
            _resolutions.Add(resolution);
            return new FPackageIndex(this, _resolutions.Count - 1);
        }

        public ResolvedObject? ResolvePackageIndex(FPackageIndex? index) =>
            index is { IsNull: false } && index.Index < _resolutions.Count ? _resolutions[index.Index] : null;

        public string Name { get; set; } = "Stub";
        public IFileProvider? Provider => null;
        public TypeMappings? Mappings => null;
        public FPackageFileSummary Summary => throw new NotSupportedException();
        public FNameEntrySerialized[] NameMap => throw new NotSupportedException();
        public int ImportMapLength => 0;
        public int ExportMapLength => 0;
        public Lazy<UObject>[] ExportsLazy => [];
        public bool IsFullyLoaded => true;
        public bool CanDeserialize => true;
        public bool HasFlags(EPackageFlags flags) => false;
        public int GetExportIndex(string name, StringComparison comparisonType = StringComparison.Ordinal) => -1;
    }

    // A reference that names its target but cannot load it, like an import whose source package
    // is not mounted.
    private sealed class UnloadableObject(IPackage package, string name) : ResolvedObject(package)
    {
        public override FName Name => new(name);
    }
}
