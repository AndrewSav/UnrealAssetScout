using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CUE4Parse.UE4.Assets.Exports;
using Newtonsoft.Json;

namespace UnrealAssetScout.Export;

// Shared path-normalization and file-writing helpers for export routines.
// Called by package and simple exporters such as VerseExporter, PackageJsonExporter, and
// SimpleExportSupport to build safe output paths and persist extracted content on disk, and to
// derive the per-export leaf and output path that keep two different exports off one file.
// JSON is streamed to disk rather than built in memory first, because a single .NET string has a
// size ceiling that the JSON of some assets exceeds.
internal static class ExportPathUtils
{
    // Matches File.WriteAllText, which wrote JSON output before it was streamed: no byte order mark,
    // and invalid UTF-16 rejected rather than silently replaced.
    private static readonly UTF8Encoding Utf8NoBomStrict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    internal static string GetPackageDirectory(string packagePath) =>
        packagePath.Contains('/') ? packagePath[..packagePath.LastIndexOf('/')] : string.Empty;

    // Walks the resolved outer chain rather than the loaded objects, because ResolvedObject exposes
    // the names without forcing every outer to deserialize. The outermost entry is the package
    // itself, which the output path already carries, so it stops one short of it.
    // Names the container the bytes were read from, so two media that CUE4Parse names alike do not
    // land on one file: its Wwise naming appends the language, which is the same for every
    // non-localised sound, rather than anything that tells one medium from another. Applied to every
    // media file rather than only where a name repeats, because which names repeat depends on what
    // else the run exported, and a file's name must not.
    internal static string ApplyOriginSuffix(string name, ArtifactOrigin? origin)
    {
        if (origin is not { } presentOrigin)
            return name;

        var container = Path.GetFileNameWithoutExtension(presentOrigin.Container);
        return string.IsNullOrEmpty(container) ? name : $"{name} [{container}]";
    }

    internal static string ComposeExportLeaf(UObject export)
    {
        var outerNames = new List<string>();
        for (var outer = export.Outer; outer?.Outer is not null; outer = outer.Outer)
            outerNames.Add(outer.Name.Text);

        outerNames.Reverse();
        outerNames.Add(export.Name);
        return string.Join('/', outerNames);
    }

    // The package name, not the export name, is what makes a flat output path unique: package
    // paths are unique, export names are not, and sibling packages routinely share one.
    internal static string ComposeExportPath(string packagePath, string? leaf, bool nestUnderPackage)
    {
        var directory = GetPackageDirectory(packagePath);
        var packageName = Path.GetFileNameWithoutExtension(packagePath);
        var relative = string.IsNullOrEmpty(directory) ? packageName : $"{directory}/{packageName}";

        if (!nestUnderPackage)
            return SanitizeRelativePath(relative);

        var normalizedLeaf = (leaf ?? string.Empty).Replace('\\', '/').Trim().Trim('/');
        return SanitizeRelativePath(
            normalizedLeaf.Length == 0 ? relative : $"{relative}/{normalizedLeaf}");
    }

    // Audio keeps this rather than ComposeExportPath: a Wwise or FMOD media name is already a full
    // logical path identifying the media itself, so rooting it under the package that referenced it
    // would repeat the directory and split media shared by several events into copies.
    internal static string ComposeRelativeAssetPath(string packagePath, string? nameOrRelativePath)
    {
        var normalized = (nameOrRelativePath ?? string.Empty).Replace('\\', '/').Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            normalized = Path.GetFileNameWithoutExtension(packagePath);

        string relative;
        if (normalized.Contains('/'))
        {
            relative = normalized.TrimStart('/');
        }
        else
        {
            var dir = GetPackageDirectory(packagePath);
            relative = string.IsNullOrEmpty(dir) ? normalized : $"{dir}/{normalized}";
        }

        return SanitizeRelativePath(relative);
    }

    internal static string ToOutputPath(string outputDir, string filePath, string extension)
    {
        var outPath = Path.Combine(outputDir, filePath.Replace('/', Path.DirectorySeparatorChar));
        return string.IsNullOrEmpty(extension) ? outPath : Path.ChangeExtension(outPath, extension);
    }

    internal static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    internal static void WriteFile(string path, byte[] content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }

    internal static void WriteJson(string path, object value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            using var textWriter = new StreamWriter(path, append: false, Utf8NoBomStrict, bufferSize: 65536);
            using var jsonWriter = new JsonTextWriter(textWriter) { Formatting = Formatting.Indented };
            var serializer = JsonSerializer.CreateDefault();
            serializer.Formatting = Formatting.Indented;
            serializer.Serialize(jsonWriter, value);
        }
        catch
        {
            DeletePartialOutput(path);
            throw;
        }
    }

    // A failed export records no output in the manifest, so a partly written file would never be
    // cleaned up by a later run and would read as a complete export.
    private static void DeletePartialOutput(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string SanitizeRelativePath(string relativePath)
    {
        var parts = relativePath.Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(SanitizePathSegment)
            .ToArray();

        return parts.Length == 0 ? "_" : string.Join('/', parts);
    }

    private static string SanitizePathSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
            return "_";

        var chars = segment.ToCharArray();
        var invalid = Path.GetInvalidFileNameChars();
        for (var i = 0; i < chars.Length; i++)
        {
            if (invalid.Contains(chars[i]))
                chars[i] = '_';
        }

        return new string(chars);
    }
}
