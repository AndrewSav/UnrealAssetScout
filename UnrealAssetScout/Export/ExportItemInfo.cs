using CUE4Parse.FileProvider.Objects;
using CUE4Parse.FileProvider.Vfs;

namespace UnrealAssetScout.Export;

internal readonly record struct ExportItemInfo(AbstractVfsFileProvider Provider, GameFile File, string GameDirectory)
{
    public string Path => File.Path;
}
