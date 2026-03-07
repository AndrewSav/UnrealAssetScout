using CUE4Parse.UE4.Assets;

namespace UnrealAssetScout.Package;

// Immutable context describing the result of preparing a package for export.
// Created by PackageLoadSupport when a package is probed or loaded for export, then consumed by
// ExportProcessor and individual exporters to inspect the package, path, usmap requirement, and
// load status without re-running package preparation.
internal readonly record struct PackageExportContext(
    IPackage? Package,
    string Path,
    UsmapRequirement Requirement,
    string Prefix,
    PackageLoadResult LoadResult);
