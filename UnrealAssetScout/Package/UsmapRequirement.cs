namespace UnrealAssetScout.Package;

// Whether a package needs usmap mappings loaded to be read, as determined by PackageLoadSupport
// from either a cheap file-level probe or, once known, the loaded package's own flags. Consumed by
// ExportProcessor, ListProcessor and RunStatsAccumulator to report and track usmap-gated packages.
internal enum UsmapRequirement
{
    Unknown,
    DoesNotRequireUsmap,
    RequiresUsmap
}
