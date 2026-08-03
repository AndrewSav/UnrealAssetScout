using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnrealAssetScout.Logging;

namespace UnrealAssetScout.Incremental;

// Finds and removes outputs the previous manifest tracked that no source in the new manifest
// claims, then prunes any directory those deletions emptied.
// Called by IncrementalRunner during COMMIT, after export completes and before the manifest is
// written, because only then is it known what each re-exported source actually produced.
// Untracked files, meaning anything in the output tree no manifest ever recorded, are left alone.
// Every candidate, whether a file to delete or a directory to prune, is resolved to an absolute
// path and checked against the output root before anything is touched, so a manifest entry that
// escapes the output tree, whether through a leading "..", or because it is already an absolute
// or UNC path, is refused rather than acted on.
internal static class OrphanCleanup
{
    internal static IReadOnlyList<string> FindOrphans(ExportManifest? previous, ExportManifest current)
    {
        if (previous is null)
            return [];

        var claimed = new HashSet<string>(
            current.Sources.Values.SelectMany(source => source.O).Select(id => current.Outputs[id]),
            StringComparer.OrdinalIgnoreCase);

        return previous.Outputs.Where(output => !claimed.Contains(output)).ToList();
    }

    internal static int Delete(string outputDir, IReadOnlyList<string> orphans)
    {
        var root = ResolveRoot(outputDir);
        var deleted = 0;
        var touchedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var orphan in orphans)
        {
            var path = Path.GetFullPath(Path.Combine(outputDir, orphan));
            if (!IsWithinRoot(root, path))
            {
                // Path.Combine discards outputDir when orphan is already rooted, and GetFullPath
                // resolves a leading "..": either way, an escaping entry is refused rather than
                // deleted, since only what the manifest actually tracked under the output tree
                // may ever be removed.
                AppLog.Warning("Refusing to delete orphaned output outside the output directory: {Path}", path);
                continue;
            }

            try
            {
                if (!File.Exists(path))
                    continue;

                File.Delete(path);
                deleted++;
                if (Path.GetDirectoryName(path) is { Length: > 0 } directory)
                    touchedDirectories.Add(directory);
            }
            catch (Exception e)
            {
                AppLog.Warning("Could not delete orphaned output {Path}: {Message}", path, e.Message);
            }
        }

        foreach (var directory in touchedDirectories)
            PruneEmptyDirectories(root, directory);

        return deleted;
    }

    // Walks up to the output root. Safe by construction: a directory holding user files is not
    // empty, so nothing a user placed in the tree is ever removed. A reparse point stops the walk
    // outright: Path.GetFullPath is lexical only, so a directory link under the root can lead
    // anywhere, and deleting it is not the same as deleting an empty directory.
    private static void PruneEmptyDirectories(string root, string directory)
    {
        var current = Path.GetFullPath(directory);

        while (!string.Equals(current, root, StringComparison.OrdinalIgnoreCase) &&
               IsWithinRoot(root, current) &&
               Directory.Exists(current) &&
               !File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint) &&
               !Directory.EnumerateFileSystemEntries(current).Any())
        {
            try
            {
                Directory.Delete(current);
            }
            catch (Exception e)
            {
                AppLog.Warning("Could not prune empty output directory {Path}: {Message}", current, e.Message);
                return;
            }

            current = Path.GetDirectoryName(current) ?? root;
        }
    }

    private static string ResolveRoot(string outputDir) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(outputDir));

    // True when candidate is root itself or lies strictly beneath it. The trailing separator on
    // the prefix check is required so a sibling directory that merely extends the root's name,
    // such as ExportOld next to Export, is never mistaken for a descendant. Shared by Delete's
    // containment check and PruneEmptyDirectories' walk so the two checks cannot drift apart.
    private static bool IsWithinRoot(string root, string candidate) =>
        string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase) ||
        candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
