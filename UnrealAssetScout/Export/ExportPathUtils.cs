using System;
using System.IO;
using System.Linq;

namespace UnrealAssetScout.Export;

// Shared path-normalization and file-writing helpers for export routines.
// Called by package and simple exporters such as VerseExporter, PackageJsonExporter, and
// SimpleExportSupport to build safe output paths and persist extracted content on disk.
internal static class ExportPathUtils
{
    internal static string GetPackageDirectory(string packagePath) =>
        packagePath.Contains('/') ? packagePath[..packagePath.LastIndexOf('/')] : string.Empty;

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
