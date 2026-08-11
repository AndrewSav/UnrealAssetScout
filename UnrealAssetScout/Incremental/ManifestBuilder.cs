using System.Collections.Generic;
using System.Linq;
using UnrealAssetScout.Utils;

namespace UnrealAssetScout.Incremental;

// Assembles the new manifest for a run, interning every string and every set exactly once.
// Called by IncrementalRunner during COMMIT: AddRecorded for each re-exported source, CarryForward
// for each unchanged one, SetFingerprint for every container entry, InternUsmap passed to SetUsmap,
// then Build.
// CarryForward re-interns rather than copying ids, because the old and new tables never agree once
// paths or types have come and gone.
internal sealed class ManifestBuilder(
    string mode,
    string game,
    IReadOnlyList<ToolVersionPair> tool,
    IReadOnlyList<string> skipTypes,
    bool scriptBytecode,
    IReadOnlyList<string> containers)
{
    private readonly StringTable _paths = new();
    private readonly StringTable _outputs = new();
    private readonly StringTable _ueTypes = new();
    private readonly StringTable _ueEnums = new();
    private readonly StringTable _clrTypes = new();
    private readonly SetTable _typeSets = new();
    private readonly SetTable _clrTypeSets = new();
    private readonly Dictionary<int, ManifestSource> _sources = [];
    private readonly Dictionary<string, string> _fingerprints = [];
    private readonly Dictionary<string, List<string>> _clrTypeChains = [];
    private ManifestUsmapBlock _usmap = new();

    internal void AddRecorded(SourceRecord record)
    {
        if (record.ClrTypeChains is { } chains)
            AddClrTypeChains(chains);

        var entry = new ManifestSource
        {
            C = record.Constituents.Select(_paths.Intern).ToList(),
            D = record.Dependencies.Select(_paths.Intern).ToList(),
            P = record.LayoutProviders.Select(_paths.Intern).ToList(),
            O = record.Outputs.Select(_outputs.Intern).ToList(),
            T = InternNameSet(record.UsmapTypes, _ueTypes, _typeSets),
            U = InternNameSet(record.UnknownTypes, _ueTypes, _typeSets),
            X = InternNameSet(record.ClrTypes, _clrTypes, _clrTypeSets),
            E = record.ExternalWwise,
            B = record.Bytecode,
            S = record.Status,
            Ms = record.Milliseconds
        };

        _sources[_paths.Intern(record.Path)] = entry;
    }

    internal void AddClrTypeChains(IReadOnlyDictionary<string, List<string>> chains)
    {
        foreach (var (leaf, chain) in chains)
            _clrTypeChains[leaf] = chain;
    }

    internal void CarryForward(string sourcePath, ExportManifest old, ManifestSource entry)
    {
        var carried = new ManifestSource
        {
            C = entry.C.Select(id => _paths.Intern(old.Paths[id])).ToList(),
            D = entry.D.Select(id => _paths.Intern(old.Paths[id])).ToList(),
            P = entry.P.Select(id => _paths.Intern(old.Paths[id])).ToList(),
            O = entry.O.Select(id => _outputs.Intern(old.Outputs[id])).ToList(),
            T = CarrySet(entry.T, old.TypeSets, old.UeTypes, _ueTypes, _typeSets),
            U = CarrySet(entry.U, old.TypeSets, old.UeTypes, _ueTypes, _typeSets),
            X = CarrySet(entry.X, old.ClrTypeSets, old.ClrTypes, _clrTypes, _clrTypeSets),
            E = entry.E,
            B = entry.B,
            S = entry.S,
            // Carried so a plan can total the cost of every source it covers, not only the ones
            // this run happened to re-export.
            Ms = entry.Ms
        };

        foreach (var leafId in entry.X is { } setId ? old.ClrTypeSets[setId] : [])
        {
            var leaf = old.ClrTypes[leafId];
            if (!old.ClrTypeChains.TryGetValue(leafId, out var chainIds))
                continue;

            _clrTypeChains[leaf] = chainIds.Select(id => old.ClrTypes[id]).ToList();
        }

        _sources[_paths.Intern(sourcePath)] = carried;
    }

    internal void SetFingerprint(string path, string hash) => _fingerprints[path] = hash;

    internal void SetUsmap(ManifestUsmapBlock usmap) => _usmap = usmap;

    // Must be called, and its result passed to SetUsmap, before Build takes its table snapshots.
    internal ManifestUsmapBlock InternUsmap(UsmapSnapshot usmap) => new()
    {
        Types = usmap.TypeFingerprints.ToDictionary(pair => _ueTypes.Intern(pair.Key), pair => pair.Value),
        Enums = usmap.EnumFingerprints.ToDictionary(pair => _ueEnums.Intern(pair.Key), pair => pair.Value)
    };

    internal ExportManifest Build()
    {
        // Interning chain names can add entries to _clrTypes, so this must run before the ClrTypes
        // snapshot below is taken, or a chain-only name would be missing from that table. Computed
        // as ordinary sequential statements rather than relying on object-initializer source order,
        // which nothing else here depends on and which a reader should not have to rely on either.
        var clrTypeChains = _clrTypeChains.ToDictionary(
            pair => _clrTypes.Intern(pair.Key),
            pair => pair.Value.Select(_clrTypes.Intern).ToList());
        var clrTypes = _clrTypes.ToList();

        return new()
        {
            Mode = mode,
            Game = game,
            Tool = [.. tool],
            UasVersion = AppVersion.DisplayText,
            SkipTypes = [.. skipTypes],
            ScriptBytecode = scriptBytecode,
            Containers = [.. containers],
            Usmap = _usmap,
            Paths = _paths.ToList(),
            Outputs = _outputs.ToList(),
            UeTypes = _ueTypes.ToList(),
            UeEnums = _ueEnums.ToList(),
            ClrTypeChains = clrTypeChains,
            ClrTypes = clrTypes,
            TypeSets = _typeSets.ToList(),
            ClrTypeSets = _clrTypeSets.ToList(),
            Fingerprints = _paths.ToList()
                .Select((path, id) => (path, id))
                .Where(pair => _fingerprints.ContainsKey(pair.path))
                .ToDictionary(pair => pair.id, pair => _fingerprints[pair.path]),
            Sources = _sources
        };
    }

    private static int? InternNameSet(IReadOnlyList<string>? names, StringTable nameTable, SetTable setTable)
    {
        if (names is null)
            return null;

        return setTable.Intern(names.Select(nameTable.Intern));
    }

    private static int? CarrySet(
        int? oldSetId,
        IReadOnlyList<List<int>> oldSets,
        IReadOnlyList<string> oldNames,
        StringTable nameTable,
        SetTable setTable)
    {
        if (oldSetId is not { } id)
            return null;

        return setTable.Intern(oldSets[id].Select(nameId => nameTable.Intern(oldNames[nameId])));
    }

    // Assigns each distinct string a stable, dense id in first-seen order.
    private sealed class StringTable
    {
        private readonly Dictionary<string, int> _ids = [];
        private readonly List<string> _values = [];

        internal int Intern(string value)
        {
            if (_ids.TryGetValue(value, out var id))
                return id;

            id = _values.Count;
            _ids[value] = id;
            _values.Add(value);
            return id;
        }

        internal List<string> ToList() => _values;
    }

    // Assigns each distinct set of ids a stable id. Members are sorted and deduplicated first, so
    // two sources that saw the same types in a different order share one set.
    private sealed class SetTable
    {
        private readonly Dictionary<string, int> _ids = [];
        private readonly List<List<int>> _values = [];

        internal int Intern(IEnumerable<int> members)
        {
            var normalized = members.Distinct().Order().ToList();
            var key = string.Join(',', normalized);
            if (_ids.TryGetValue(key, out var id))
                return id;

            id = _values.Count;
            _ids[key] = id;
            _values.Add(normalized);
            return id;
        }

        internal List<List<int>> ToList() => _values;
    }
}
