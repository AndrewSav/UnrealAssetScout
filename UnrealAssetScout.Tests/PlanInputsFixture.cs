using UnrealAssetScout.Incremental;

namespace UnrealAssetScout.Tests;

// Builds PlanInputs with sensible defaults so each planner test states only what it is testing.
internal static class PlanInputsFixture
{
    internal const string Mode = "json";
    internal const string Game = "GAME_UE5_1";

    internal static readonly ToolVersionPair Tool = new(1, "bbb");

    internal static PlanInputs Create(
        ExportManifest? manifest = null,
        IReadOnlyDictionary<string, SourceCandidate>? sources = null,
        IReadOnlyDictionary<string, string>? fingerprints = null,
        UsmapSnapshot? usmap = null,
        IReadOnlyList<string>? skipTypes = null,
        bool scriptBytecode = false,
        IReadOnlyList<string>? containers = null,
        Func<string, bool>? outputExists = null,
        bool rebuild = false,
        bool acceptToolVersion = false,
        string mode = Mode,
        string game = Game,
        ToolVersionPair? tool = null,
        Func<string, string?>? resolvePackagePath = null) =>
        new(
            Manifest: manifest,
            Mode: mode,
            Game: game,
            Tool: tool ?? Tool,
            Containers: containers ?? ["a.pak"],
            SkipTypes: skipTypes ?? [],
            ScriptBytecode: scriptBytecode,
            Sources: sources ?? new Dictionary<string, SourceCandidate>(),
            Fingerprints: fingerprints ?? new Dictionary<string, string>(),
            Usmap: usmap ?? UsmapSnapshot.Empty,
            OutputExists: outputExists ?? (_ => true),
            Rebuild: rebuild,
            AcceptToolVersion: acceptToolVersion,
            ResolvePackagePath: resolvePackagePath ?? (_ => null));

    internal static Dictionary<string, SourceCandidate> Sources(params string[] paths) =>
        paths.ToDictionary(path => path, path => new SourceCandidate(path, [path]));

    // A manifest with one entry per given path, each with itself as its only constituent,
    // one output, and fingerprints matching Fingerprints(paths).
    internal static ExportManifest Manifest(params string[] paths)
    {
        var manifest = new ExportManifest
        {
            Mode = Mode,
            Game = Game,
            Tool = [Tool],
            Containers = ["a.pak"]
        };

        foreach (var path in paths)
        {
            var pathId = manifest.Paths.Count;
            manifest.Paths.Add(path);
            var outputId = manifest.Outputs.Count;
            manifest.Outputs.Add(path + ".json");
            manifest.Fingerprints[pathId] = "hash-of-" + path;
            manifest.Sources[pathId] = new ManifestSource
            {
                C = [pathId], D = [], O = [outputId],
                B = BytecodeState.False, S = SourceStatus.Ok
            };
        }

        return manifest;
    }

    internal static Dictionary<string, string> Fingerprints(params string[] paths) =>
        paths.ToDictionary(path => path, path => "hash-of-" + path);
}
