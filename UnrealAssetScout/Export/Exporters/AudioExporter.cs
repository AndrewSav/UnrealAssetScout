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
using CUE4Parse.UE4.CriWare;
using CUE4Parse.UE4.FMod;
using CUE4Parse.UE4.Wwise;
using UnrealAssetScout.Package;

namespace UnrealAssetScout.Export.Exporters;

// Exports supported Unreal audio assets to decoded or extracted audio files on disk.
// Called by ExportProcessor audio-mode handlers when a package export matches one of the supported
// audio asset types.
internal static class AudioExporter
{
    internal static ExportAttemptResult TryExport(UObject export, ExportItemInfo item, PackageExportContext packageContext, string outputDir)
    {
        try
        {
            var packagePath = packageContext.Path;

            return export switch
            {
                UExternalSource { Data.WemFile: { Length: > 0 } wemFile } externalSource =>
                    TrySaveAudioFile(
                        packagePath,
                        outputDir,
                        string.IsNullOrWhiteSpace(externalSource.ExternalSourcePath)
                            ? export.Name
                            : Path.GetFileNameWithoutExtension(externalSource.ExternalSourcePath),
                        "wem",
                        wemFile,
                        export.Name),
                UAkAudioBank audioBank => TryExportWwiseBank(audioBank, item, packagePath, outputDir),
                UAkAudioEvent audioEvent => TryExportWwiseEvent(audioEvent, item, packagePath, outputDir),
                UFMODEvent fmodEvent => TryExportFmodEvent(fmodEvent, item, packagePath, outputDir),
                UFMODBank fmodBank => TryExportFmodBank(fmodBank, item, packagePath, outputDir),
                USoundAtomCueSheet cueSheet => TryExportCriWare(cueSheet, item, packagePath, outputDir),
                UAtomCueSheet cueSheet => TryExportCriWare(cueSheet, item, packagePath, outputDir),
                USoundAtomCue cue => TryExportCriWare(cue, item, packagePath, outputDir),
                UAtomWaveBank atomWaveBank => TryExportCriWare(atomWaveBank, item, packagePath, outputDir),
                UAkMediaAsset mediaAsset when mediaAsset.CurrentMediaAssetData?.TryLoad<UAkMediaAssetData>(out var mediaAssetData) is true =>
                    TryExportDecodedAudio(packagePath, outputDir, mediaAssetData, mediaAsset.MediaName, export.Name),
                UAkAudioEventData eventData => TryExportAudioEventData(eventData, packagePath, outputDir),
                UMidiFile midiFile => TryExportMidi(packagePath, outputDir, midiFile),
                USoundWave or UAkMediaAssetData =>
                    TryExportDecodedAudio(packagePath, outputDir, export, export.Name, export.Name),
                _ => ExportAttemptResult.NotHandled()
            };
        }
        catch (Exception e)
        {
            return ExportAttemptResult.Failure($"{packageContext.Path}/{export.Name}", e.Message);
        }
    }

    private static ExportAttemptResult TryExportWwiseBank(UAkAudioBank audioBank, ExportItemInfo item, string packagePath, string outputDir)
    {
        var wwiseProvider = AudioProviderFactory.GetProvider<WwiseProvider>(item);
        if (wwiseProvider is null)
            return ExportAttemptResult.NotHandled();

        return TrySaveAudioFiles(
            packagePath,
            outputDir,
            audioBank.Name,
            wwiseProvider.ExtractBankSounds(audioBank).Select(sound => (sound.OutputPath, sound.Extension, sound.Data)));
    }

    private static ExportAttemptResult TryExportWwiseEvent(UAkAudioEvent audioEvent, ExportItemInfo item, string packagePath, string outputDir)
    {
        var wwiseProvider = AudioProviderFactory.GetProvider<WwiseProvider>(item);
        if (wwiseProvider is null)
            return ExportAttemptResult.NotHandled();

        return TrySaveAudioFiles(
            packagePath,
            outputDir,
            audioEvent.Name,
            wwiseProvider.ExtractAudioEventSounds(audioEvent).Select(sound => (sound.OutputPath, sound.Extension, sound.Data)));
    }

    private static ExportAttemptResult TryExportFmodEvent(UFMODEvent fmodEvent, ExportItemInfo item, string packagePath, string outputDir)
    {
        var fmodProvider = AudioProviderFactory.GetProvider<FModProvider>(item);
        if (fmodProvider is null)
            return ExportAttemptResult.NotHandled();

        return TrySaveAudioFiles(
            packagePath,
            outputDir,
            fmodEvent.Name,
            fmodProvider.ExtractEventSounds(fmodEvent).Select(sound => (sound.Name, sound.Extension, sound.Data)));
    }

    private static ExportAttemptResult TryExportFmodBank(UFMODBank fmodBank, ExportItemInfo item, string packagePath, string outputDir)
    {
        var fmodProvider = AudioProviderFactory.GetProvider<FModProvider>(item);
        if (fmodProvider is null)
            return ExportAttemptResult.NotHandled();

        return TrySaveAudioFiles(
            packagePath,
            outputDir,
            fmodBank.Name,
            fmodProvider.ExtractBankSounds(fmodBank).Select(sound => (sound.Name, sound.Extension, sound.Data)));
    }

    private static ExportAttemptResult TryExportCriWare(USoundAtomCueSheet cueSheet, ExportItemInfo item, string packagePath, string outputDir)
    {
        var criWareProvider = AudioProviderFactory.GetProvider<CriWareProvider>(item);
        if (criWareProvider is null)
            return ExportAttemptResult.NotHandled();

        return TrySaveAudioFiles(
            packagePath,
            outputDir,
            cueSheet.Name,
            criWareProvider.ExtractCriWareSounds(cueSheet).Select(sound => (sound.Name, sound.Extension, sound.Data)));
    }

    private static ExportAttemptResult TryExportCriWare(UAtomCueSheet cueSheet, ExportItemInfo item, string packagePath, string outputDir)
    {
        var criWareProvider = AudioProviderFactory.GetProvider<CriWareProvider>(item);
        if (criWareProvider is null)
            return ExportAttemptResult.NotHandled();

        return TrySaveAudioFiles(
            packagePath,
            outputDir,
            cueSheet.Name,
            criWareProvider.ExtractCriWareSounds(cueSheet).Select(sound => (sound.Name, sound.Extension, sound.Data)));
    }

    private static ExportAttemptResult TryExportCriWare(USoundAtomCue cue, ExportItemInfo item, string packagePath, string outputDir)
    {
        var criWareProvider = AudioProviderFactory.GetProvider<CriWareProvider>(item);
        if (criWareProvider is null)
            return ExportAttemptResult.NotHandled();

        return TrySaveAudioFiles(
            packagePath,
            outputDir,
            cue.Name,
            criWareProvider.ExtractCriWareSounds(cue).Select(sound => (sound.Name, sound.Extension, sound.Data)));
    }

    private static ExportAttemptResult TryExportCriWare(UAtomWaveBank atomWaveBank, ExportItemInfo item, string packagePath, string outputDir)
    {
        var criWareProvider = AudioProviderFactory.GetProvider<CriWareProvider>(item);
        if (criWareProvider is null)
            return ExportAttemptResult.NotHandled();

        return TrySaveAudioFiles(
            packagePath,
            outputDir,
            atomWaveBank.Name,
            criWareProvider.ExtractCriWareSounds(atomWaveBank).Select(sound => (sound.Name, sound.Extension, sound.Data)));
    }

    private static ExportAttemptResult TryExportAudioEventData(UAkAudioEventData eventData, string packagePath, string outputDir)
    {
        var exportedArtifacts = new List<ExportedArtifact>();
        foreach (var mediaIndex in eventData.MediaList)
        {
            if (!mediaIndex.TryLoad<UAkMediaAsset>(out var mediaAsset) ||
                mediaAsset.CurrentMediaAssetData?.TryLoad<UAkMediaAssetData>(out var mediaAssetData) is not true)
                continue;

            var exportResult = TryExportDecodedAudio(packagePath, outputDir, mediaAssetData, mediaAsset.MediaName, eventData.Name);
            if (!exportResult.Succeeded)
                continue;

            exportedArtifacts.AddRange(exportResult.ExportedArtifacts);
        }

        return ExportAttemptResult.Success(exportedArtifacts);
    }

    private static ExportAttemptResult TryExportDecodedAudio(string packagePath, string outputDir, UObject export, string? preferredName, string exportNameForLog)
    {
        export.Decode(false, out var audioFormat, out var data);
        if (data is null || string.IsNullOrWhiteSpace(audioFormat))
            return ExportAttemptResult.NotHandled();

        var audioName = string.IsNullOrWhiteSpace(preferredName) ? exportNameForLog : preferredName;
        return TrySaveAudioFile(packagePath, outputDir, audioName, audioFormat, data, exportNameForLog);
    }

    private static ExportAttemptResult TrySaveAudioFiles(string packagePath, string outputDir, string exportNameForLog,
        IEnumerable<(string Name, string Extension, byte[] Data)> extracted)
    {
        var exportedArtifacts = new List<ExportedArtifact>();
        foreach (var (name, extension, data) in extracted)
        {
            var exportResult = TrySaveAudioFile(packagePath, outputDir, name, extension, data, exportNameForLog);
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

    private static ExportAttemptResult TrySaveAudioFile(string packagePath, string outputDir, string? name, string? extension, byte[]? data, string exportNameForLog)
    {
        if (data is null || data.Length == 0 || string.IsNullOrWhiteSpace(extension))
            return ExportAttemptResult.NotHandled();

        var relativePath = ExportPathUtils.ComposeRelativeAssetPath(packagePath, name);
        var outPath = ExportPathUtils.ToOutputPath(outputDir, relativePath, "." + extension.TrimStart('.').ToLowerInvariant());
        ExportPathUtils.WriteFile(outPath, data);
        return ExportAttemptResult.Success($"{packagePath}/{exportNameForLog}", outPath);
    }
}
