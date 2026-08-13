using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace UnrealAssetScout.Incremental;

// Loads and saves the export manifest at the root of an output directory.
// Called by IncrementalRunner: TryLoad before planning, Save as the very last step of a run so a
// crash leaves the manifest describing the last known good state. Save is atomic via a temp file
// and a rename, so a half-written manifest is never observable.
internal static class ExportManifestStore
{
    internal const string FileName = ".uas-manifest.json";

    // Must match ExportManifest.Schema's default. System.Text.Json silently ignores unknown
    // properties and defaults missing ones, so a manifest written under a future format change
    // would otherwise load as a partially-defaulted manifest of this shape instead of failing, and
    // the damage would land on the staleness rules rather than being reported here.
    // Bump this only when defaulting a field could change what a plan decides. Adding a field the
    // planner never reads does not qualify: an older manifest still plans identically, and every
    // existing dump would otherwise have to be rebuilt for nothing.
    internal const int CurrentSchema = 2;

    // Indented so the global block at the top can be read when reviewing how a run was
    // configured; ManifestSourceConverter keeps each source entry on one line so that block is
    // not buried under, and the file not doubled by, the entries below it.
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // Cosmetic: keeps punctuation out of \uXXXX form.
        Converters = { new ManifestSourceConverter() }
    };

    internal static string PathFor(string outputDir) => Path.Combine(outputDir, FileName);

    internal static ExportManifest? TryLoad(string outputDir, out string? error)
    {
        error = null;
        var path = PathFor(outputDir);
        if (!File.Exists(path))
            return null;

        try
        {
            var manifest = JsonSerializer.Deserialize<ExportManifest>(File.ReadAllText(path), SerializerOptions);
            if (manifest is null)
            {
                error = $"manifest at {path} is empty";
                return null;
            }

            if (manifest.Schema != CurrentSchema)
            {
                error = $"manifest at {path} has schema {manifest.Schema}, this build expects schema " +
                    $"{CurrentSchema}; pass --rebuild to replace it";
                return null;
            }

            return manifest;
        }
        catch (Exception e)
        {
            error = $"manifest at {path} could not be parsed: {e.Message}; pass --rebuild to replace it";
            return null;
        }
    }

    internal static void Save(string outputDir, ExportManifest manifest)
    {
        Directory.CreateDirectory(outputDir);
        var path = PathFor(outputDir);
        var tempPath = path + ".tmp";

        File.WriteAllText(tempPath, JsonSerializer.Serialize(manifest, SerializerOptions));
        File.Move(tempPath, path, overwrite: true);
    }
}
