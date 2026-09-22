using System;
using System.Collections.Generic;
using System.IO;

namespace UnrealAssetScout.Incremental;

// Answers ExportPlanner's "does this recorded output still exist" from one listing of the output
// directory, taken on first use, instead of one file system call per output.
// Created by IncrementalRunner and handed to the planner as PlanInputs.OutputExists. A name the
// listing does not hold is checked with File.Exists before it is reported missing, so every answer
// is the one File.Exists would give; the listing only makes the common answer, yes, cheap.
internal sealed class ExistingOutputs(string outputDir)
{
    private HashSet<string>? _listed;

    internal bool Contains(string relativePath) =>
        (_listed ??= List()).Contains(relativePath) || File.Exists(Path.Combine(outputDir, relativePath));

    // Ordinal, so a name cased differently from the file on disk falls through to File.Exists,
    // which knows whether that directory is case-sensitive.
    private HashSet<string> List()
    {
        var listed = new HashSet<string>(StringComparer.Ordinal);
        if (!Directory.Exists(outputDir))
            return listed;

        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0 };
        foreach (var path in Directory.EnumerateFiles(outputDir, "*", options))
            listed.Add(Path.GetRelativePath(outputDir, path));

        return listed;
    }
}
