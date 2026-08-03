using System;
using System.Collections.Generic;
using System.Linq;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Objects.UObject;

namespace UnrealAssetScout.Incremental;

// Walks each export's class chain to find which usmap names the package's deserialization
// consulted. Called by SourceRecorder for every exported package.
// A live chain level takes its layout from its own package, never from the usmap, so live names
// are not recorded; their packages are covered by dependency fingerprints instead. The usmap is
// consulted exactly where the live walk ends (unversioned layout hands over to the mappings at a
// script class, and ConstructObject's chain repair reads the ending level's own entry by name)
// and for property references that did not resolve to a live loaded object, mirroring the
// live-first preference in PropertyType and IndexToEnum. Names the usmap could not answer are
// classified separately, so a later usmap that gains them re-exports the package.
// The walk also reports, through layoutProviders, the owning packages of every live struct and
// enum it saw: the packages whose layouts this package's output embeds, which the planner uses as
// its staleness propagation edges.
internal static class TypeChainWalker
{
    internal static TypeChainResult Walk(
        IEnumerable<UObject> exports, UsmapSnapshot usmap, ICollection<string>? layoutProviders = null)
    {
        var consulted = new List<string>();

        foreach (var export in exports)
        {
            // Mirrors AbstractUePackage.ConstructObject's own chain walk. Every export already
            // forced this same walk during construction, so it reaches no further than what was
            // already loaded and cached; only the property scan below is additional work.
            var current = export.Class?.Load<UStruct>();
            var seen = new HashSet<string>();

            while (current is not null)
            {
                if (!seen.Add(current.Name))
                    break;

                if (current.Owner is { } ownerPackage)
                    layoutProviders?.Add(ownerPackage.Name);

                // Null on a script class, built from a name and never deserialized, where every chain ends.
                // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
                foreach (var property in (current.ChildProperties ?? []).OfType<FProperty>())
                    CollectPropertyReferences(property, consulted, layoutProviders);

                // Null on a script class, built from a name and never deserialized, where every chain ends.
                // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
                var super = current.SuperStruct?.Load<UStruct>();
                if (super is null)
                {
                    consulted.Add(current.Name);
                    break;
                }

                current = super;
            }
        }

        return Classify(consulted, usmap);
    }

    // FPackageIndex.Name reads the export or import table entry directly, never the lazily loaded
    // object, so naming a reference never forces a deserialization. Deciding whether a reference
    // resolved live does read the lazily loaded object, which unversioned schema construction has
    // already forced for every property this walk can see.
    private static void CollectPropertyReferences(
        FProperty property, List<string> consulted, ICollection<string>? layoutProviders)
    {
        switch (property)
        {
            case FStructProperty { Struct.ResolvedObject: { } resolvedStruct }:
                if (TakesStructLayoutFromUsmap(resolvedStruct))
                    consulted.Add(resolvedStruct.Name.Text);
                else if (resolvedStruct.Object?.Value.Owner is { } structOwner)
                    layoutProviders?.Add(structOwner.Name);
                break;
            case FByteProperty { Enum.ResolvedObject: { } resolvedByteEnum }:
                if (TakesEnumNamesFromUsmap(resolvedByteEnum))
                    consulted.Add(resolvedByteEnum.Name.Text);
                else if (resolvedByteEnum.Object?.Value.Owner is { } byteEnumOwner)
                    layoutProviders?.Add(byteEnumOwner.Name);
                break;
            case FEnumProperty { Enum.ResolvedObject: { } resolvedEnum }:
                if (TakesEnumNamesFromUsmap(resolvedEnum))
                    consulted.Add(resolvedEnum.Name.Text);
                else if (resolvedEnum.Object?.Value.Owner is { } enumOwner)
                    layoutProviders?.Add(enumOwner.Name);
                break;
            case FArrayProperty { Inner: { } inner }:
                CollectPropertyReferences(inner, consulted, layoutProviders);
                break;
            case FSetProperty { ElementProp: { } element }:
                CollectPropertyReferences(element, consulted, layoutProviders);
                break;
            case FMapProperty map:
                if (map.KeyProp is { } key) CollectPropertyReferences(key, consulted, layoutProviders);
                if (map.ValueProp is { } value) CollectPropertyReferences(value, consulted, layoutProviders);
                break;
            case FOptionalProperty { ValueProperty: { } optional }:
                CollectPropertyReferences(optional, consulted, layoutProviders);
                break;
        }
    }

    // Mirrors FScriptStruct: a struct value is read with the live loaded struct when there is one,
    // and by name through the usmap when there is none or only a script placeholder.
    private static bool TakesStructLayoutFromUsmap(ResolvedObject resolved) =>
        resolved.Object?.Value as UStruct is null or UScriptClass;

    // Mirrors EnumProperty.IndexToEnum: a live UEnum names the values; anything else falls back to
    // the usmap's enum table.
    private static bool TakesEnumNamesFromUsmap(ResolvedObject resolved) =>
        resolved.Object?.Value is not UEnum;

    // Ordinal, like UsmapFingerprints's own sets: these are engine-authored identities, not text
    // for a human, and the default comparer can treat ordinally distinct names as compare-equal,
    // which a SortedSet then silently drops as a duplicate.
    internal static TypeChainResult Classify(IReadOnlyList<string> consulted, UsmapSnapshot usmap)
    {
        var known = new SortedSet<string>(StringComparer.Ordinal);
        var stopped = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var name in consulted)
        {
            if (usmap.TypeFingerprints.ContainsKey(name) || usmap.EnumFingerprints.ContainsKey(name))
                known.Add(name);
            else
                stopped.Add(name);
        }

        return new TypeChainResult([.. known], [.. stopped]);
    }
}
