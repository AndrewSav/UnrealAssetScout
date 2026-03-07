namespace UnrealAssetScout.Export;

// Identifies one exported output file and the source-style path that should be used in log messages.
// Created by exporter helpers and returned inside ExportAttemptResult so ExportProcessor can log
// successful exports consistently across simple and graphics modes.
internal readonly record struct ExportedArtifact(string LogPath, string OutputPath);
