namespace UnrealAssetScout.Export;

// Carries both the specialized simple-export attempt and the raw-file fallback result.
// Returned by SimpleFileExporter to ExportProcessor so simple-mode logging and stats can stay in
// the processor while the exporter owns the actual file-export work.
internal readonly record struct SimpleFileExportResult(
    ExportAttemptResult SpecializedResult,
    ExportAttemptResult RawFallbackResult,
    string StatKey);
