using System.Collections.Generic;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.FileProvider.Vfs;

namespace UnrealAssetScout.Utils;

// Every mounted path once, as the entry the provider itself resolves it to. Used wherever the whole
// file set is walked: ExportProcessor, ListProcessor, and SourceFingerprintIndex and
// IncrementalRunner during PLAN, so all of them see the same copy of a path.
// FileProviderDictionary.Keys and Values both enumerate every mounted container's own path set in
// turn, highest read order first, so walking either directly yields a path shadowed by a patch
// container once per container that mounts it.
internal static class ProviderFiles
{
    internal static IEnumerable<GameFile> Resolved(AbstractVfsFileProvider provider)
    {
        foreach (var path in new HashSet<string>(provider.Files.Keys, provider.PathComparer))
            yield return provider.Files[path];
    }
}
