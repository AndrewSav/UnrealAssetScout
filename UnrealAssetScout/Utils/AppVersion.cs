using System;
using System.Linq;
using System.Reflection;

namespace UnrealAssetScout.Utils;

// The identity of the running build, read once at startup: its version, the git revisions it and
// CUE4Parse were built from, and which release flavour produced it. Used by ConfigOptionsSupport
// for --version, by Program for the log file header, and by Incremental.IncrementalRunner for the
// manifest tool gate, so that every place reporting a version reports the same one.
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

    // The manifest tool gate matches this verbatim against what a manifest already records.
    internal static string UasVersionText => $"{VersionText}+{UasGitSha}";

    // For example "0.3.0+1c9b714 (self-contained)".
    internal static string DisplayText => $"{VersionText}+{UasGitSha} ({BuildFlavor ?? "local build"})";

    private static string? ReadMetadata(string key) =>
        typeof(AppVersion).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, key, StringComparison.Ordinal))
            ?.Value is { Length: > 0 } value
            ? value
            : null;
}
