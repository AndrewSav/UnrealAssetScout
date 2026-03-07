using System.Diagnostics;
using CUE4Parse.UE4.Assets.Exports;
using UnrealAssetScout.Logging;
using UnrealAssetScout.Package;
using UnrealAssetScout.Statistics;

namespace UnrealAssetScout.Export.Processors;

// Shared base class for package-mode processors.
// Created by ExportProcessor.ProcessFiles for the selected export mode, then used by
// ExportProcessor.ProcessPackageMode to provide common logging and mode-stats helpers.
internal abstract class PackageModeProcessorBase(string outputDir, bool verbose, ModeStatsAccumulator? modeStats)
{
    protected string OutputDir { get; } = outputDir;
    protected bool Verbose { get; } = verbose;
    protected ModeStatsAccumulator? ModeStats { get; } = modeStats;

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

    protected void LogFailure(PackageExportContext packageContext, ExportAttemptResult exportResult) =>
        AppLog.Warning("[FAILED]   {Prefix}{Path}: {Reason}", packageContext.Prefix, exportResult.FailurePath, exportResult.FailureReason);

    protected void LogExport(PackageExportContext packageContext, ExportedArtifact exportedArtifact) =>
        AppLog.Information("[EXPORTED] {Prefix}{Path} -> {OutPath}", packageContext.Prefix, exportedArtifact.LogPath, exportedArtifact.OutputPath);

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
