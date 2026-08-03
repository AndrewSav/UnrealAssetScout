using System;
using System.Collections.Generic;
using System.Linq;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.UE4.FMod;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Wwise;
using UnrealAssetScout.Incremental;

namespace UnrealAssetScout.Export.Exporters;

// Extracts individual sound files from Wwise and FMOD bank containers.
// Called by ExportProcessor simple-mode handlers after AudioProviderFactory supplies the middleware-specific
// provider needed to read the selected bank file. When a SourceRecorder is supplied, also records
// the Wwise media provenance for a standalone bank file the same way AudioExporter does for one
// read from a package.
internal static class AudioBankExporter
{
    internal static ExportAttemptResult TryExport<TProvider>(ExportItemInfo item, string outputDir, SourceRecorder? recorder)
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
                    WwiseProvider wwise => ExtractWwiseSounds(wwise, archive, item, recorder),
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

    private static IEnumerable<(string, string, byte[])> ExtractWwiseSounds(
        WwiseProvider wwise, FArchive archive, ExportItemInfo item, SourceRecorder? recorder)
    {
        var sounds = wwise.ExtractBankSounds(new WwiseReader(new FWwiseArchive(archive), new WwiseGameFileSource(item.File)));
        RecordWwiseProvenance(sounds, recorder);
        return sounds.Select(sound => (SimpleExportSupport.NormalizeRelativePath(sound.OutputPath), sound.Extension, sound.GetData()));
    }

    // Mirrors AudioExporter's own Wwise provenance capture; keep the two in sync.
    private static void RecordWwiseProvenance(IEnumerable<WwiseExtractedSound> sounds, SourceRecorder? recorder)
    {
        if (recorder is null)
            return;

        foreach (var sound in sounds)
        {
            if (sound.Data is not FGameFileDeferredByteData gameFileData)
                continue;

            if (gameFileData.File is OsGameFile)
                recorder.MarkExternalWwise();
            else
                recorder.AddMediaDependency(gameFileData.File.Path);
        }
    }
}
