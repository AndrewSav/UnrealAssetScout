using System;
using System.Linq;
using System.Reflection;
using CUE4Parse.UE4.Pak.Objects;

namespace UnrealAssetScout.Incremental;

// The version pair for the currently running build, read once from assembly metadata stamped by
// the StampGitRevisions MSBuild target. Called by IncrementalRunner when building PlanInputs.
internal static class ToolVersions
{
    internal static ToolVersionPair Current { get; } = Build();

    private static ToolVersionPair Build()
    {
        var uasAssembly = typeof(ToolVersions).Assembly;
        var cue4ParseAssembly = typeof(FPakEntry).Assembly;

        return new ToolVersionPair(
            $"{GetVersion(uasAssembly)}+{GetMetadata(uasAssembly, "UasGitSha")}",
            $"{GetVersion(cue4ParseAssembly)}+{GetMetadata(uasAssembly, "Cue4ParseGitSha")}");
    }

    private static string GetVersion(Assembly assembly) =>
        assembly.GetName().Version?.ToString() ?? "0.0.0.0";

    private static string GetMetadata(Assembly assembly, string key) =>
        assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, key, StringComparison.Ordinal))
            ?.Value is { Length: > 0 } value
            ? value
            : "unknown";
}
