namespace UnrealAssetScout.Package;

// Outcome of attempting to load a package through PackageLoadSupport. Carried on
// PackageExportContext and inspected by ExportProcessor to decide whether a package's contents
// can be processed or the attempt should be reported as a failure.
internal enum PackageLoadResult
{
    Success,
    FailureRequiresUsmap,
    FailureOther
}
