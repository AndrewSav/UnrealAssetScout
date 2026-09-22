namespace UnrealAssetScout.Incremental;

// A pak entry whose inline header holds its stored hash, built by SourceFingerprintIndex and read by
// PakInlineHeaderBatchReader. A null ContainerPath means the pak is not a plain file on disk, so only
// the fallback can read the entry.
internal readonly record struct PakInlineHeaderRequest(
    string? ContainerPath,
    long Offset,
    long CompressedSize,
    long UncompressedSize,
    int HashOffset);
