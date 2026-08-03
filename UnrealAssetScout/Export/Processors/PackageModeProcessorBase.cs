using System.Collections.Generic;
using System.Diagnostics;
using CUE4Parse.UE4.Assets.Exports;
using UnrealAssetScout.Logging;
using UnrealAssetScout.Package;
using UnrealAssetScout.Statistics;

namespace UnrealAssetScout.Export.Processors;

// Shared base class for package-mode processors.
// Created by ExportProcessor.ProcessFiles for the selected export mode, then used by
// ExportProcessor.ProcessPackageMode to provide common logging and mode-stats helpers, to
// collect every artifact it exports so the caller can hand them to a SourceRecorder, and to
// record whether any export attempt failed so ExportProcessor.ResolveStatus can tell a package
// that produced nothing but errors from one that legitimately had nothing to export.
internal abstract class PackageModeProcessorBase(string outputDir, bool verbose, ModeStatsAccumulator? modeStats)
{
    private readonly List<ExportedArtifact> _recordedArtifacts = [];

    protected string OutputDir { get; } = outputDir;
    protected bool Verbose { get; } = verbose;
    protected ModeStatsAccumulator? ModeStats { get; } = modeStats;

    internal IReadOnlyList<ExportedArtifact> RecordedArtifacts => _recordedArtifacts;

    // Set by LogFailure, so it catches a failure regardless of which processor logs it: this
    // class's own ProcessPackage loop, or a whole-package failure a subclass logs directly.
    internal bool HasFailure { get; private set; }

    public virtual void ProcessPackage(PackageExportContext packageContext)
    {
        var exported = false;
        foreach (var export in packageContext.Package!.GetExports())
        {
            var exportResult = TryExport(export, packageContext);

            if (exportResult.Failed)
                LogFailure(packageContext, exportResult);

            if (!exportResult.Succeeded)
                continue;

            RecordExportHit(packageContext, exportResult);
            exported = true;
        }

        if (!exported && Verbose)
            AppLog.Information("[SKIPPED]  {Prefix}{Path} ({Reason})", packageContext.Prefix, packageContext.Path, NoExportsReason);
    }

    protected void LogFailure(PackageExportContext packageContext, ExportAttemptResult exportResult)
    {
        HasFailure = true;
        AppLog.Warning("[FAILED]   {Prefix}{Path}: {Reason}", packageContext.Prefix, exportResult.FailurePath, exportResult.FailureReason);
    }

    protected void LogExport(PackageExportContext packageContext, ExportedArtifact exportedArtifact)
    {
        _recordedArtifacts.Add(exportedArtifact);
        AppLog.Information("[EXPORTED] {Prefix}{Path} -> {OutPath}", packageContext.Prefix, exportedArtifact.LogPath, exportedArtifact.OutputPath);
    }

    protected void RecordExportHit(PackageExportContext packageContext, ExportAttemptResult exportResult)
    {
        Debug.Assert(ModeStats != null, nameof(ModeStats) + " != null");
        foreach (var exportedArtifact in exportResult.ExportedArtifacts)
        {
            LogExport(packageContext, exportedArtifact);
            ModeStats.RecordHit("count");
        }
    }

    protected virtual ExportAttemptResult TryExport(UObject export, PackageExportContext packageContext) =>
        ExportAttemptResult.NotHandled();

    protected virtual string NoExportsReason => "no supported exports";
}
