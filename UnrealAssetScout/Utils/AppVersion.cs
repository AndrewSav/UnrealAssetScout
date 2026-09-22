using System;
using System.Linq;
using System.Reflection;

namespace UnrealAssetScout.Utils;

// The identity of the running build, read once at startup: its version, the git revisions it and
// CUE4Parse were built from, and which release flavour produced it. Used by BuildVersionAction for
// --version, by Program for the log file header, by SelfUpdate to decide whether to update and by
// UpdateCommand to report it, and by the incremental manifest: ManifestBuilder records DisplayText
// as uasVersion and IncrementalRunner puts Cue4ParseGitSha in the tool gate's pair. Every place
// that reports a version therefore reports the same one.
internal static class AppVersion
{
    private const string UnknownRevision = "unknown";

    // So that the updater knows which build flavour to fetch
    internal static string? BuildFlavor { get; } = ReadMetadata("BuildFlavor");

    internal static bool IsPublishedBuild => BuildFlavor is not null;

    internal static Version Current { get; } =
        typeof(AppVersion).Assembly.GetName().Version ?? new Version(0, 0, 0);

    // GitHub releases and user facing versions are 3 part, not 4
    internal static string VersionText => Current.ToString(3);

    internal static string UasGitSha { get; } = ReadMetadata("UasGitSha") ?? UnknownRevision;

    internal static string Cue4ParseGitSha { get; } = ReadMetadata("Cue4ParseGitSha") ?? UnknownRevision;

    // For example "0.3.0+1c9b714 (self-contained)".
    internal static string DisplayText => $"{VersionText}+{UasGitSha} ({BuildFlavor ?? "local build"})";

    private static string? ReadMetadata(string key) =>
        typeof(AppVersion).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, key, StringComparison.Ordinal))
            ?.Value is { Length: > 0 } value
            ? value
            : null;
}
