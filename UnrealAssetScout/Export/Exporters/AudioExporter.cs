using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CUE4Parse_Conversion.Sounds;
using CUE4Parse.GameTypes.SMG.UE4.Assets.Exports.Wwise;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.CriWare;
using CUE4Parse.UE4.Assets.Exports.Fmod;
using CUE4Parse.UE4.Assets.Exports.Harmonix;
using CUE4Parse.UE4.Assets.Exports.Sound;
using CUE4Parse.UE4.Assets.Exports.Wwise;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.UE4.CriWare;
using CUE4Parse.UE4.FMod;
using CUE4Parse.UE4.Wwise;
using UnrealAssetScout.Incremental;
using UnrealAssetScout.Package;

namespace UnrealAssetScout.Export.Exporters;

// Exports supported Unreal audio assets to decoded or extracted audio files on disk.
// Called by ExportProcessor audio-mode handlers when a package export matches one of the supported
// audio asset types. When a SourceRecorder is supplied, also records the container-path provenance
// of any Wwise media a bank or event export reads, or marks the source as reading external Wwise
// media when it does not have one.
internal static class AudioExporter
{
    internal static ExportAttemptResult TryExport(UObject export, ExportItemInfo item, PackageExportContext packageContext, string outputDir, SourceRecorder? recorder, bool disambiguate)
    {
        try
        {
            var packagePath = packageContext.Path;

            return export switch
            {
                UExternalSource { Data.WemFile: { IsValid: true } wemFile } externalSource =>
                    TrySaveAudioFile(
                        packagePath,
                        outputDir,
                        string.IsNullOrWhiteSpace(externalSource.ExternalSourcePath)
                            ? export.Name
                            : Path.GetFileNameWithoutExtension(externalSource.ExternalSourcePath),
                        "wem",
                        wemFile.GetData(),
                        export.Name,
                        disambiguate),
                UAkAudioBank audioBank => TryExportWwiseBank(audioBank, item, packagePath, outputDir, recorder, disambiguate),
                UAkAudioEvent audioEvent => TryExportWwiseEvent(audioEvent, item, packagePath, outputDir, recorder, disambiguate),
                UFMODEvent fmodEvent => TryExportFmodEvent(fmodEvent, item, packagePath, outputDir, disambiguate),
                UFMODBank fmodBank => TryExportFmodBank(fmodBank, item, packagePath, outputDir, disambiguate),
                USoundAtomCueSheet cueSheet => TryExportCriWare(cueSheet, item, packagePath, outputDir, disambiguate),
                UAtomCueSheet cueSheet => TryExportCriWare(cueSheet, item, packagePath, outputDir, disambiguate),
                USoundAtomCue cue => TryExportCriWare(cue, item, packagePath, outputDir, disambiguate),
                UAtomWaveBank atomWaveBank => TryExportCriWare(atomWaveBank, item, packagePath, outputDir, disambiguate),
                UAkMediaAsset mediaAsset when mediaAsset.CurrentMediaAssetData?.TryLoad<UAkMediaAssetData>(out var mediaAssetData) is true =>
                    TryExportDecodedAudio(packagePath, outputDir, mediaAssetData, mediaAsset.MediaName, export.Name, disambiguate),
                UAkAudioEventData eventData => TryExportAudioEventData(eventData, packagePath, outputDir, disambiguate),
                UMidiFile midiFile => TryExportMidi(packagePath, outputDir, midiFile),
                USoundWave or UAkMediaAssetData =>
                    TryExportDecodedAudio(packagePath, outputDir, export, export.Name, export.Name, disambiguate),
                _ => ExportAttemptResult.NotHandled()
            };
        }
        catch (Exception e)
        {
            return ExportAttemptResult.Failure($"{packageContext.Path}/{export.Name}", e.Message);
        }
    }

    private static ExportAttemptResult TryExportWwiseBank(UAkAudioBank audioBank, ExportItemInfo item, string packagePath, string outputDir, SourceRecorder? recorder, bool disambiguate)
    {
        var wwiseProvider = AudioProviderFactory.GetProvider<WwiseProvider>(item);
        if (wwiseProvider is null)
            return ExportAttemptResult.NotHandled();

        var sounds = wwiseProvider.ExtractBankSounds(audioBank);
        RecordWwiseProvenance(sounds, recorder);

        return TrySaveAudioFiles(
            packagePath,
            outputDir,
            audioBank.Name,
            disambiguate,
            sounds.Select(sound => (sound.OutputPath, sound.Extension, Data: sound.GetData(), Origin: OriginOf(sound))));
    }

    private static ExportAttemptResult TryExportWwiseEvent(UAkAudioEvent audioEvent, ExportItemInfo item, string packagePath, string outputDir, SourceRecorder? recorder, bool disambiguate)
    {
        var wwiseProvider = AudioProviderFactory.GetProvider<WwiseProvider>(item);
        if (wwiseProvider is null)
            return ExportAttemptResult.NotHandled();

        var sounds = wwiseProvider.ExtractAudioEventSounds(audioEvent);
        RecordWwiseProvenance(sounds, recorder);

        return TrySaveAudioFiles(
            packagePath,
            outputDir,
            audioEvent.Name,
            disambiguate,
            sounds.Select(sound => (sound.OutputPath, sound.Extension, Data: sound.GetData(), Origin: OriginOf(sound))));
    }

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

    private static ExportAttemptResult TryExportFmodEvent(UFMODEvent fmodEvent, ExportItemInfo item, string packagePath, string outputDir, bool disambiguate)
    {
        var fmodProvider = AudioProviderFactory.GetProvider<FModProvider>(item);
        if (fmodProvider is null)
            return ExportAttemptResult.NotHandled();

        return TrySaveAudioFiles(
            packagePath,
            outputDir,
            fmodEvent.Name,
            disambiguate,
            fmodProvider.ExtractEventSounds(fmodEvent).Select(sound => (sound.Name, sound.Extension, sound.Data, (ArtifactOrigin?) null)));
    }

    private static ExportAttemptResult TryExportFmodBank(UFMODBank fmodBank, ExportItemInfo item, string packagePath, string outputDir, bool disambiguate)
    {
        var fmodProvider = AudioProviderFactory.GetProvider<FModProvider>(item);
        if (fmodProvider is null)
            return ExportAttemptResult.NotHandled();

        return TrySaveAudioFiles(
            packagePath,
            outputDir,
            fmodBank.Name,
            disambiguate,
            fmodProvider.ExtractBankSounds(fmodBank).Select(sound => (sound.Name, sound.Extension, sound.Data, (ArtifactOrigin?) null)));
    }

    private static ExportAttemptResult TryExportCriWare(USoundAtomCueSheet cueSheet, ExportItemInfo item, string packagePath, string outputDir, bool disambiguate)
    {
        var criWareProvider = AudioProviderFactory.GetProvider<CriWareProvider>(item);
        if (criWareProvider is null)
            return ExportAttemptResult.NotHandled();

        return TrySaveAudioFiles(
            packagePath,
            outputDir,
            cueSheet.Name,
            disambiguate,
            criWareProvider.ExtractCriWareSounds(cueSheet).Select(sound => (sound.Name, sound.Extension, sound.Data, (ArtifactOrigin?) null)));
    }

    private static ExportAttemptResult TryExportCriWare(UAtomCueSheet cueSheet, ExportItemInfo item, string packagePath, string outputDir, bool disambiguate)
    {
        var criWareProvider = AudioProviderFactory.GetProvider<CriWareProvider>(item);
        if (criWareProvider is null)
            return ExportAttemptResult.NotHandled();

        return TrySaveAudioFiles(
            packagePath,
            outputDir,
            cueSheet.Name,
            disambiguate,
            criWareProvider.ExtractCriWareSounds(cueSheet).Select(sound => (sound.Name, sound.Extension, sound.Data, (ArtifactOrigin?) null)));
    }

    private static ExportAttemptResult TryExportCriWare(USoundAtomCue cue, ExportItemInfo item, string packagePath, string outputDir, bool disambiguate)
    {
        var criWareProvider = AudioProviderFactory.GetProvider<CriWareProvider>(item);
        if (criWareProvider is null)
            return ExportAttemptResult.NotHandled();

        return TrySaveAudioFiles(
            packagePath,
            outputDir,
            cue.Name,
            disambiguate,
            criWareProvider.ExtractCriWareSounds(cue).Select(sound => (sound.Name, sound.Extension, sound.Data, (ArtifactOrigin?) null)));
    }

    private static ExportAttemptResult TryExportCriWare(UAtomWaveBank atomWaveBank, ExportItemInfo item, string packagePath, string outputDir, bool disambiguate)
    {
        var criWareProvider = AudioProviderFactory.GetProvider<CriWareProvider>(item);
        if (criWareProvider is null)
            return ExportAttemptResult.NotHandled();

        return TrySaveAudioFiles(
            packagePath,
            outputDir,
            atomWaveBank.Name,
            disambiguate,
            criWareProvider.ExtractCriWareSounds(atomWaveBank).Select(sound => (sound.Name, sound.Extension, sound.Data, (ArtifactOrigin?) null)));
    }

    private static ExportAttemptResult TryExportAudioEventData(UAkAudioEventData eventData, string packagePath, string outputDir, bool disambiguate)
    {
        var exportedArtifacts = new List<ExportedArtifact>();
        foreach (var mediaIndex in eventData.MediaList)
        {
            if (!mediaIndex.TryLoad<UAkMediaAsset>(out var mediaAsset) ||
                mediaAsset.CurrentMediaAssetData?.TryLoad<UAkMediaAssetData>(out var mediaAssetData) is not true)
                continue;

            var exportResult = TryExportDecodedAudio(packagePath, outputDir, mediaAssetData, mediaAsset.MediaName, eventData.Name, disambiguate);
            if (!exportResult.Succeeded)
                continue;

            exportedArtifacts.AddRange(exportResult.ExportedArtifacts);
        }

        return ExportAttemptResult.Success(exportedArtifacts);
    }

    private static ExportAttemptResult TryExportDecodedAudio(string packagePath, string outputDir, UObject export, string? preferredName, string exportNameForLog, bool disambiguate)
    {
        export.Decode(false, out var audioFormat, out var data);
        if (data is null || string.IsNullOrWhiteSpace(audioFormat))
            return ExportAttemptResult.NotHandled();

        var audioName = string.IsNullOrWhiteSpace(preferredName) ? exportNameForLog : preferredName;
        return TrySaveAudioFile(packagePath, outputDir, audioName, audioFormat, data, exportNameForLog, disambiguate);
    }

    private static ExportAttemptResult TrySaveAudioFiles(string packagePath, string outputDir, string exportNameForLog, bool disambiguate,
        IEnumerable<(string Name, string Extension, byte[] Data, ArtifactOrigin? Origin)> extracted)
    {
        var exportedArtifacts = new List<ExportedArtifact>();
        foreach (var (name, extension, data, origin) in extracted)
        {
            var exportResult = TrySaveAudioFile(packagePath, outputDir, name, extension, data, exportNameForLog, disambiguate, origin);
            if (!exportResult.Succeeded)
                continue;

            exportedArtifacts.AddRange(exportResult.ExportedArtifacts);
        }

        return ExportAttemptResult.Success(exportedArtifacts);
    }

    private static ExportAttemptResult TryExportMidi(string packagePath, string outputDir, UMidiFile midiFile)
    {
        var data = midiFile.Export();
        var outPath = ExportPathUtils.ToOutputPath(
            outputDir,
            ExportPathUtils.ComposeRelativeAssetPath(packagePath, midiFile.Name),
            ".mid");
        ExportPathUtils.WriteFile(outPath, data);
        return ExportAttemptResult.Success($"{packagePath}/{midiFile.Name}", outPath);
    }

    private static ExportAttemptResult TrySaveAudioFile(string packagePath, string outputDir, string? name, string? extension, byte[]? data, string exportNameForLog, bool disambiguate, ArtifactOrigin? origin = null)
    {
        if (data is null || data.Length == 0 || string.IsNullOrWhiteSpace(extension))
            return ExportAttemptResult.NotHandled();

        var relativePath = ExportPathUtils.ComposeRelativeAssetPath(packagePath, disambiguate ? ExportPathUtils.ApplyOriginSuffix(name ?? string.Empty, origin) : name);
        var outPath = ExportPathUtils.ToOutputPath(outputDir, relativePath, "." + extension.TrimStart('.').ToLowerInvariant());
        ExportPathUtils.WriteFile(outPath, data);
        return ExportAttemptResult.Success([new ExportedArtifact($"{packagePath}/{exportNameForLog}", outPath, origin)]);
    }

    // A media file is identified by the container entry it lives in together with its own path
    // inside that entry: one bank or package entry holds many media, and one media is reached by
    // many events, so neither half identifies it alone.
    private static ArtifactOrigin? OriginOf(WwiseExtractedSound sound) =>
        sound.Data is FGameFileDeferredByteData gameFileData
            ? new ArtifactOrigin(gameFileData.File.Path, sound.OutputPath)
            : null;
}
