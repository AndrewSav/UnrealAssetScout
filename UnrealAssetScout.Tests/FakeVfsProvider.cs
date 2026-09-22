using CUE4Parse.FileProvider.Vfs;

namespace UnrealAssetScout.Tests;

// A provider with nothing mounted, so a test can add container file sets straight to its Files
// dictionary, each at its own read order. Used by tests that need a path shadowed by a patch container.
internal sealed class FakeVfsProvider(StringComparer pathComparer) : AbstractVfsFileProvider(pathComparer: pathComparer)
{
    public override void Initialize() { }
}
