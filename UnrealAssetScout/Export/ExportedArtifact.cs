namespace UnrealAssetScout.Export;

// Identifies one exported output file and the source-style path that should be used in log
// messages. Created by exporter helpers and returned inside ExportAttemptResult so ExportProcessor
// can log successful exports consistently across simple and package modes.
internal readonly record struct ExportedArtifact(string LogPath, string OutputPath, ArtifactOrigin? Origin = null)
{
    // Where the bytes came from, which is what separates deduplication from a naming collision: two
    // artifacts on one output path are the same file only when they came from the same place. An
    // exporter reading out of a container supplies that container; everything else is identified by
    // its export, which is unique on its own.
    internal ArtifactOrigin OriginOrExport => Origin ?? new ArtifactOrigin(LogPath, null);
}
