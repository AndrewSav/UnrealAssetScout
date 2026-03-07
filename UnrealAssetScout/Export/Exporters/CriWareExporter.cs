using System;
using System.Linq;
using CUE4Parse.UE4.CriWare;
using CUE4Parse.UE4.CriWare.Readers;

namespace UnrealAssetScout.Export.Exporters;

// Extracts audio payloads from CriWare ACB and AWB archives.
// Called by ExportProcessor simple-mode handlers after AudioProviderFactory creates the shared CriWareProvider
// used to decode the selected archive.
internal static class CriWareExporter
{
    internal static ExportAttemptResult TryExport<TReader>(ExportItemInfo item, string outputDir)
        where TReader : class
    {
        try
        {
            var criWareProvider = AudioProviderFactory.GetProvider<CriWareProvider>(item);
            if (criWareProvider is null || !item.File.TryCreateReader(out var archive))
                return ExportAttemptResult.NotHandled();

            using (archive)
            {
                var reader = (TReader)Activator.CreateInstance(typeof(TReader), archive)!;
                var directory = SimpleExportSupport.NormalizeRelativeDirectory(archive.Name);
                var sounds = reader switch
                {
                    AcbReader acb => criWareProvider.ExtractCriWareSounds(acb, archive.Name),
                    AwbReader awb => criWareProvider.ExtractCriWareSounds(awb, archive.Name),
                    _ => throw new InvalidOperationException($"Unsupported reader type: {typeof(TReader)}")
                };

                return SimpleExportSupport.SaveExtractedFiles(
                    outputDir, item.Path,
                    sounds.Select(s => (SimpleExportSupport.CombineRelativePath(directory, s.Name), s.Extension, s.Data)));
            }
        }
        catch (Exception e)
        {
            return ExportAttemptResult.Failure(item.Path, e.Message);
        }
    }
}
