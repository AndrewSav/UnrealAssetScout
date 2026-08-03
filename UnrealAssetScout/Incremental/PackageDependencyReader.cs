using System.Collections.Generic;
using System.Linq;
using CUE4Parse.FileProvider.Vfs;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.IO.Objects;

namespace UnrealAssetScout.Incremental;

// Reads a package's package-level imports as stable dependency identities, without deserializing
// anything. Called by SourceRecorder for every exported package to populate the manifest's `d`.
// Identities rather than container paths, so that an import which does not resolve today keeps
// the same value if a later patch adds its target: that is the edge that stops such a package
// escaping invalidation when its class chain suddenly reaches further.
// Pak and IoStore expose imports differently, so each has its own path here. Neither loads the
// imported package.
internal static class PackageDependencyReader
{
    // CUE4Parse.UE4.Assets.Package is written out in full below: its short name collides with the
    // sibling UnrealAssetScout.Package namespace, which the compiler prefers over the using-directive
    // import.
    internal static List<string> Read(IPackage package, AbstractVfsFileProvider provider) => package switch
    {
        CUE4Parse.UE4.Assets.Package pakPackage => ReadPakImports(pakPackage),
        IoPackage ioPackage => ReadIoStoreImports(ioPackage, provider),
        _ => []
    };

    // Package-level imports are the entries whose class is "Package".
    private static List<string> ReadPakImports(CUE4Parse.UE4.Assets.Package package) =>
        package.ImportMap
            .Where(import => import.ClassName.Text == "Package")
            .Select(import => import.ObjectName.Text)
            .Distinct()
            .Order()
            .ToList();

    private static List<string> ReadIoStoreImports(IoPackage package, AbstractVfsFileProvider provider)
    {
        var storeEntry = provider.TryFindStoreEntry(FPackageId.FromName(package.Name));
        if (storeEntry?.ImportedPackages is not { } importedIds)
            return [];

        return importedIds
            .Select(id => "packageid:" + id.id)
            .Distinct()
            .Order()
            .ToList();
    }
}
