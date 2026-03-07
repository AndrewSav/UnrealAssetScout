using CUE4Parse.UE4.Oodle.Objects;

namespace UnrealAssetScout.Export.Exporters;

// Serializes Oodle dictionary metadata to JSON for simple exports.
// Called by ExportProcessor when an Oodle dictionary file is selected for export and the archive header
// should be written to the output directory as structured JSON.
internal static class OodleDictionaryExporter
{
    internal static ExportAttemptResult TryExport(ExportItemInfo item, string outputDir) =>
        SimpleExportSupport.TryExportJson(item, outputDir,
            archive => new FOodleDictionaryArchive(archive).Header);
}
