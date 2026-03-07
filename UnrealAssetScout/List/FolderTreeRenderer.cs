using System.Collections.Generic;

namespace UnrealAssetScout.List;

// Reconstructs a folder-only ASCII tree from mounted file paths because CUE4Parse exposes files
// but not directories. Called by ListProcessor when list mode runs with the Tree format to emit
// each folder name alongside the count of its immediate file children.
internal static class FolderTreeRenderer
{
    internal static IReadOnlyList<string> RenderFolders(IEnumerable<string> filePaths)
    {
        var root = new FolderNode(string.Empty);
        foreach (var filePath in filePaths)
        {
            var parts = filePath.Split('/', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);
            if (parts.Length < 2)
                continue;

            var current = root;
            for (var i = 0; i < parts.Length - 1; i++)
                current = current.GetOrAddChild(parts[i]);

            current.ImmediateFileCount++;
        }

        if (root.Children.Count == 0)
            return [];

        var lines = new List<string> { "." };
        AppendChildren(root, string.Empty, lines);
        return lines;
    }

    private static void AppendChildren(FolderNode parent, string prefix, List<string> lines)
    {
        var childIndex = 0;
        foreach (var child in parent.Children.Values)
        {
            var isLast = childIndex == parent.Children.Count - 1;
            lines.Add(prefix + (isLast ? "`-- " : "|-- ") + FormatFolderLabel(child));
            AppendChildren(child, prefix + (isLast ? "    " : "|   "), lines);
            childIndex++;
        }
    }

    private static string FormatFolderLabel(FolderNode folder)
        => $"{folder.Name} ({folder.ImmediateFileCount} {(folder.ImmediateFileCount == 1 ? "file" : "files")})";

    private sealed class FolderNode(string name)
    {
        internal string Name { get; } = name;
        internal SortedDictionary<string, FolderNode> Children { get; } = new(System.StringComparer.OrdinalIgnoreCase);
        internal int ImmediateFileCount { get; set; }

        internal FolderNode GetOrAddChild(string name)
        {
            if (Children.TryGetValue(name, out var existingChild))
                return existingChild;

            var child = new FolderNode(name);
            Children.Add(name, child);
            return child;
        }
    }
}
