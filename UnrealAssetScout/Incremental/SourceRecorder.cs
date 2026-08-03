using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CUE4Parse.FileProvider.Vfs;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Objects.UObject;
using UnrealAssetScout.Export;

namespace UnrealAssetScout.Incremental;

// Collects, for each source ExportProcessor actually exports, everything the next run's planner
// will need: which files it was made of, what it produced, what it imported, which usmap types it
// consulted and where it was blocked, and whether it had script bytecode.
// Created by IncrementalRunner and threaded through ExportProcessor. One record per source, opened
// by BeginSource and closed by EndSource; the records are then handed to ManifestBuilder.
internal sealed class SourceRecorder(string outputDir, UsmapSnapshot usmap, bool scriptBytecode, bool isJsonMode)
{
    private readonly List<SourceRecord> _records = [];
    private Pending? _current;

    internal IReadOnlyList<SourceRecord> Records => _records;

    internal void BeginSource(string path, IReadOnlyList<string> constituents) =>
        _current = new Pending(path, [.. constituents]);

    internal void AddArtifacts(IEnumerable<ExportedArtifact> artifacts)
    {
        var pending = Require();
        foreach (var artifact in artifacts)
            pending.Outputs.Add(Path.GetRelativePath(outputDir, artifact.OutputPath));
    }

    internal void AddMediaDependency(string containerPath) => Require().Dependencies.Add(containerPath);

    internal void MarkExternalWwise() => Require().ExternalWwise = true;

    internal void ObservePackage(IPackage package, AbstractVfsFileProvider provider)
    {
        var pending = Require();
        var exports = package.GetExports().ToList();

        pending.Dependencies.UnionWith(PackageDependencyReader.Read(package, provider));

        var layoutProviders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var chain = TypeChainWalker.Walk(exports, usmap, layoutProviders);
        pending.UsmapTypes = [.. chain.Known];
        pending.UnknownTypes = [.. chain.Stopped];

        // A package always provides layouts to itself; that edge carries no information the
        // planner can use, so it never reaches the manifest.
        var extensionStart = pending.Path.LastIndexOf('.');
        var selfStem = extensionStart >= 0 ? pending.Path[..extensionStart] : pending.Path;
        foreach (var layoutProvider in layoutProviders)
        {
            if (!layoutProvider.Equals(selfStem, StringComparison.OrdinalIgnoreCase))
                pending.LayoutProviders.Add(layoutProvider);
        }

        if (isJsonMode)
        {
            var clrTypeInfo = BuildClrTypeInfo(exports.Select(export => export.GetType()));
            pending.ClrTypes = clrTypeInfo.ClrTypes;
            pending.ClrTypeChains = clrTypeInfo.ClrTypeChains;
        }

        // Only observable with the flag on: with it off, CUE4Parse reads serializedScriptSize into
        // a local and skips past it, leaving no trace of whether the package had script data.
        if (scriptBytecode)
        {
            pending.Bytecode = exports.OfType<UStruct>().Any(structure => structure.ScriptBytecode is { Length: > 0 })
                ? BytecodeState.True
                : BytecodeState.False;
        }
    }

    internal void EndSource(string status)
    {
        var pending = Require();

        _records.Add(new SourceRecord
        {
            Path = pending.Path,
            Constituents = pending.Constituents,
            Dependencies = [.. pending.Dependencies.Order()],
            LayoutProviders = [.. pending.LayoutProviders],
            Outputs = [.. pending.Outputs],
            UsmapTypes = pending.UsmapTypes,
            UnknownTypes = pending.UnknownTypes,
            ClrTypes = pending.ClrTypes,
            ClrTypeChains = pending.ClrTypeChains,
            ExternalWwise = pending.ExternalWwise,
            // Rounded because the manifest is read by a human and summed across a whole plan;
            // sub-microsecond digits are noise in both uses.
            Milliseconds = Math.Round(pending.Elapsed.Elapsed.TotalMilliseconds, 3),
            Bytecode = isJsonMode ? pending.Bytecode : BytecodeState.False,
            Status = status
        });

        _current = null;
    }

    internal static (List<string> ClrTypes, Dictionary<string, List<string>> ClrTypeChains) BuildClrTypeInfo(
        IEnumerable<Type> types)
    {
        var typeList = types as IReadOnlyCollection<Type> ?? types.ToList();
        var distinctByName = typeList.DistinctBy(type => type.Name).ToList();

        var clrTypes = distinctByName.Select(type => type.Name).Order().ToList();
        var clrTypeChains = distinctByName.ToDictionary(type => type.Name, BaseChainOf);

        return (clrTypes, clrTypeChains);
    }

    // Mirrors JsonPackageProcessor.IsSpecialized's own walk and stopping condition exactly, so a
    // chain recorded here always terminates where that skip-list check would terminate.
    private static List<string> BaseChainOf(Type type)
    {
        var chain = new List<string>();
        for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
            chain.Add(current.Name);

        return chain;
    }

    private Pending Require() =>
        _current ?? throw new InvalidOperationException("BeginSource must be called before recording a source");

    private sealed class Pending(string path, List<string> constituents)
    {
        // Started at construction, so it spans exactly the work ExportProcessor does between
        // BeginSource and EndSource for this one source, in every mode.
        internal Stopwatch Elapsed { get; } = Stopwatch.StartNew();
        internal string Path { get; } = path;
        internal List<string> Constituents { get; } = constituents;

        // Ordinal, like TypeChainWalker.Classify's own sets: a culture-sensitive default comparer
        // can treat ordinally distinct dependency identities as equal, and SortedSet then silently
        // drops the second as a duplicate, losing a dependency edge.
        internal SortedSet<string> Dependencies { get; } = new(StringComparer.Ordinal);
        internal SortedSet<string> LayoutProviders { get; } = new(StringComparer.Ordinal);
        internal HashSet<string> Outputs { get; } = [];
        internal List<string>? UsmapTypes { get; set; }
        internal List<string>? UnknownTypes { get; set; }
        internal List<string>? ClrTypes { get; set; }
        internal Dictionary<string, List<string>>? ClrTypeChains { get; set; }
        internal bool ExternalWwise { get; set; }
        internal string Bytecode { get; set; } = BytecodeState.Unknown;
    }
}
