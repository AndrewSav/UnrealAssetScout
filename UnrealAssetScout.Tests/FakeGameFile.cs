using CUE4Parse.Compression;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Readers;

namespace UnrealAssetScout.Tests;

// A container entry whose bytes are supplied by the test, or that cannot be read or opened at all
// when none are. Paired with FakeVfsProvider.
internal sealed class FakeGameFile(string path, byte[]? content = null) : GameFile(path, content?.Length ?? 0)
{
    public override bool IsEncrypted => false;
    public override CompressionMethod CompressionMethod => CompressionMethod.None;
    public override byte[] Read(FByteBulkDataHeader? header = null) => content ?? throw new NotImplementedException();
    public override FArchive CreateReader(FByteBulkDataHeader? header = null) =>
        content is null ? throw new NotImplementedException() : new FByteArchive(Path, content);
}
