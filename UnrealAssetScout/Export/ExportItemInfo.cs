using CUE4Parse.FileProvider.Objects;
using CUE4Parse.FileProvider.Vfs;

namespace UnrealAssetScout.Export;

// Bundles the provider, file and game directory needed to export a single asset. Built by
// ExportProcessor for each candidate file and passed down into the exporters and package mode
// processors that turn it into output.
internal readonly record struct ExportItemInfo(AbstractVfsFileProvider Provider, GameFile File, string GameDirectory)
{
    public string Path => File.Path;
}
