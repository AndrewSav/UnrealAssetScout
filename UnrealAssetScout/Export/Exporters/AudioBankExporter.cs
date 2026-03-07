using System;
using System.Linq;
using CUE4Parse.UE4.FMod;
using CUE4Parse.UE4.Wwise;

namespace UnrealAssetScout.Export.Exporters;

// Extracts individual sound files from Wwise and FMOD bank containers.
// Called by ExportProcessor simple-mode handlers after AudioProviderFactory supplies the middleware-specific
// provider needed to read the selected bank file.
internal static class AudioBankExporter
{
    internal static ExportAttemptResult TryExport<TProvider>(ExportItemInfo item, string outputDir)
        where TProvider : class
    {
        try
        {
            var provider = AudioProviderFactory.GetProvider<TProvider>(item);
            if (provider is null || !item.File.TryCreateReader(out var archive))
                return ExportAttemptResult.NotHandled();

            using (archive)
            {
                var sounds = provider switch
                {
                    WwiseProvider wwise => wwise.ExtractBankSounds(new WwiseReader(archive))
                        .Select(s => (SimpleExportSupport.NormalizeRelativePath(s.OutputPath), s.Extension, s.Data)),
                    FModProvider fmod when fmod.TryLoadBank(archive, item.File.NameWithoutExtension, out var fmodReader) =>
                        fmod.ExtractBankSounds(fmodReader)
                            .Select(s => (SimpleExportSupport.CombineRelativePath(
                                SimpleExportSupport.NormalizeRelativeDirectory(item.Path), s.Name), s.Extension, s.Data)),
                    FModProvider => null,
                    _ => throw new InvalidOperationException($"Unsupported audio bank provider type: {provider.GetType()}")
                };

                return sounds is null
                    ? ExportAttemptResult.NotHandled()
                    : SimpleExportSupport.SaveExtractedFiles(outputDir, item.Path, sounds);
            }
        }
        catch (Exception e)
        {
            return ExportAttemptResult.Failure(item.Path, e.Message);
        }
    }
}
