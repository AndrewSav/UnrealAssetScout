using System;
using System.Collections.Generic;

namespace UnrealAssetScout.Export;

// Describes whether an exporter succeeded, failed, or declined to handle a file, together with
// any exported output paths. Returned by simple and graphics exporters to ExportProcessor so the
// processor can own runtime logging policy while helpers stay focused on export work.
internal readonly record struct ExportAttemptResult(
    ExportAttemptStatus Status,
    string FailurePath,
    string FailureReason,
    IReadOnlyList<ExportedArtifact> ExportedArtifacts)
{
    internal bool Succeeded => Status == ExportAttemptStatus.Succeeded;
    internal bool Failed => Status == ExportAttemptStatus.Failed;

    internal static ExportAttemptResult NotHandled() =>
        new(ExportAttemptStatus.NotHandled, string.Empty, string.Empty, Array.Empty<ExportedArtifact>());

    internal static ExportAttemptResult Failure(string path, string reason) =>
        new(ExportAttemptStatus.Failed, path, reason, Array.Empty<ExportedArtifact>());

    internal static ExportAttemptResult Success(string logPath, string outputPath) =>
        new(ExportAttemptStatus.Succeeded, string.Empty, string.Empty, [new ExportedArtifact(logPath, outputPath)]);

    internal static ExportAttemptResult Success(IReadOnlyList<ExportedArtifact> exportedArtifacts) =>
        exportedArtifacts.Count == 0
            ? NotHandled()
            : new(ExportAttemptStatus.Succeeded, string.Empty, string.Empty, exportedArtifacts);
}
