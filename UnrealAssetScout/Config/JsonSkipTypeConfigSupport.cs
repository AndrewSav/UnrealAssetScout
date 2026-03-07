using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnrealAssetScout.Logging;

namespace UnrealAssetScout.Config;

// Resolves the built-in JSON skip-type defaults plus inline and file-based overrides.
// Called by ConfigOptionsSupport while building Options for export json runs.
internal static class JsonSkipTypeConfigSupport
{
    internal static IReadOnlyList<string> DefaultTypeNames { get; } =
    [
        "UTexture",
        "USvgAsset",
        "UExternalSource",
        "UAkAudioBank",
        "UAkAudioEvent",
        "UFMODEvent",
        "UFMODBank",
        "USoundAtomCueSheet",
        "UAtomCueSheet",
        "USoundAtomCue",
        "UAtomWaveBank",
        "UAkMediaAsset",
        "UAkAudioEventData",
        "USoundWave",
        "UAkMediaAssetData",
        "UAnimSequenceBase",
        "UAnimMontage",
        "UAnimComposite",
        "USkeletalMesh",
        "UStaticMesh",
        "USkeleton",
        "ALandscapeProxy",
        "UMidiFile"
    ];

    internal static bool TryReadTypeFile(string filePath, out IReadOnlyList<string> resolvedTypeNames)
    {
        try
        {
            var fileContents = File.ReadAllText(filePath);
            resolvedTypeNames = SplitTypeNames(fileContents);
            return true;
        }
        catch (Exception e)
        {
            AppLog.Error("Failed to read JSON skip types file '{Path}': {Message}", filePath, e.Message);
            resolvedTypeNames = DefaultTypeNames;
            return false;
        }
    }

    private static IReadOnlyList<string> SplitTypeNames(string rawText)
        => Regex
            .Split(rawText, @"[,\s]+")
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
}
