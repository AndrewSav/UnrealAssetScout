using System.Collections.Generic;
using System.Linq;

namespace UnrealAssetScout.Incremental;

// Expands a usmap type name into every type and enum name reachable from it through supertypes and
// property references, then answers whether a recorded type set touches the changed-type set.
// Created by ExportPlanner once per run and queried per source. Two memoisation layers keep it
// cheap: closures per distinct name, then the intersection verdict per distinct recorded set.
// The visited set is also the cycle guard: usmap is flat and cannot express namespaced same-named
// structs, so a valid parent-child relationship can appear as a self-reference.
internal sealed class UsmapClosure(UsmapSnapshot snapshot)
{
    private readonly Dictionary<string, IReadOnlySet<string>> _byName = [];
    private readonly Dictionary<(string Names, IReadOnlySet<string> Changed), bool> _bySet = [];

    internal IReadOnlySet<string> Of(string name)
    {
        if (_byName.TryGetValue(name, out var cached))
            return cached;

        // CUE4Parse builds Types under OrdinalIgnoreCase, so a Super or referenced name can differ
        // in case from the name it is ultimately compared against. This comparer is the one place
        // that mismatch, and a case-differing enum reference, gets caught.
        var visited = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        queue.Enqueue(name);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current))
                continue;

            if (!snapshot.Types.TryGetValue(current, out var node))
                continue;

            if (node.Super is { } super)
                queue.Enqueue(super);

            foreach (var referenced in node.ReferencedTypes)
                queue.Enqueue(referenced);

            // Enums are leaves: they have no properties and no supertype to follow.
            foreach (var referencedEnum in node.ReferencedEnums)
                visited.Add(referencedEnum);
        }

        _byName[name] = visited;
        return visited;
    }

    internal bool IntersectsAny(IReadOnlyList<string> names, IReadOnlySet<string> changed)
    {
        if (names.Count == 0 || changed.Count == 0)
            return false;

        // Changed is part of the key too: within one planner run it is always the same reference,
        // so this costs nothing there, but it keeps the cache correct if two different changed
        // sets are ever compared against the same recorded names, as the unit tests do.
        var key = (string.Join(' ', names.Order()), changed);
        if (_bySet.TryGetValue(key, out var cached))
            return cached;

        var result = names.Any(name => Of(name).Any(changed.Contains));
        _bySet[key] = result;
        return result;
    }
}
