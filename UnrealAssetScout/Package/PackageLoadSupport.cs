using System;
using CUE4Parse.FileProvider;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.IO.Objects;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;

namespace UnrealAssetScout.Package;

// Centralizes package-loading and usmap-detection logic shared by listing and export workflows.
// Called from ListProcessor and export processors to determine whether a package can be loaded,
// whether mappings are required, and which status prefix should be logged for each file.
internal static class PackageLoadSupport
{
    // This is called from list/export processors to load the package when file-level signals are insufficient.
    internal static PackageExportContext PreparePackageForExport(AbstractFileProvider provider, GameFile file, bool markUsmap)
    {
        var path = file.Path;
        // Start with the cheapest usmap signal we have from the file itself so we can avoid package loads
        // when the header already tells us mappings are required or definitely not required.
        var requirement = GetUsmapRequirement(file);
        var hasMappings = provider.MappingsForGame is not null;
        var prefix = GetUsmapPrefix(markUsmap, requirement);

        // If the file-level probe already says mappings are required and the provider has none loaded,
        // fail early instead of attempting a package load that can only reproduce the same outcome.
        if (requirement == UsmapRequirement.RequiresUsmap && !hasMappings)
            return new PackageExportContext(null, path, requirement, prefix, PackageLoadResult.FailureRequiresUsmap);

        var loadResult = TryLoadPackage(provider, file);
        if (loadResult.Result != PackageLoadResult.Success)
        {
            // Preserve an explicit "requires usmap" load failure when one occurs, otherwise fall back to the
            // requirement inferred from the file probe so callers still get the best available status label.
            var loadFailureRequirement = loadResult.Result == PackageLoadResult.FailureRequiresUsmap
                ? UsmapRequirement.RequiresUsmap
                : requirement;
            return new PackageExportContext(null, path, loadFailureRequirement,
                GetLoadFailurePrefix(markUsmap, hasMappings, loadResult.Result), loadResult.Result);
        }

        var pkg = loadResult.Package!;
        if (requirement == UsmapRequirement.Unknown)
        {
            // Some files do not expose enough header information up front, so once the package loads we inspect
            // the loaded package flags and replace the unknown requirement with the authoritative value.
            requirement = GetUsmapRequirement(pkg);
            prefix = GetUsmapPrefix(markUsmap, requirement);
        }

        return new PackageExportContext(pkg, path, requirement, prefix, PackageLoadResult.Success);
    }

    // This is called from no-mode listing and export flows to determine whether a package requires usmap.
    internal static UsmapRequirement NeedsUsmap(AbstractFileProvider provider, GameFile file)
    {
        var requirement = GetUsmapRequirement(file);
        if (requirement != UsmapRequirement.Unknown)
            return requirement;

        var loadResult = TryLoadPackage(provider, file);
        if (loadResult.Result == PackageLoadResult.FailureRequiresUsmap)
            return UsmapRequirement.RequiresUsmap;

        return loadResult.Package is not null
            ? GetUsmapRequirement(loadResult.Package)
            : UsmapRequirement.DoesNotRequireUsmap;
    }

    // This is called from export processors to prepare a package and, if successful, invoke
    // mode-specific processing without duplicating the load-result branching at each callsite.
    internal static PackageExportContext ProcessPackage(
        AbstractFileProvider provider,
        GameFile file,
        bool markUsmap,
        Action<PackageExportContext> processPackage)
    {
        var packageContext = PreparePackageForExport(provider, file, markUsmap);
        if (packageContext.LoadResult == PackageLoadResult.Success)
            processPackage(packageContext);

        return packageContext;
    }

    internal static string GetUsmapPrefix(bool markUsmap, UsmapRequirement requirement)
    {
        if (!markUsmap)
            return "";

        return requirement switch
        {
            UsmapRequirement.RequiresUsmap => "[*] ",
            UsmapRequirement.DoesNotRequireUsmap => "[ ] ",
            _ => throw new ArgumentOutOfRangeException(nameof(requirement), requirement, "Unsupported usmap requirement")
        };
    }

    private static (IPackage? Package, PackageLoadResult Result) TryLoadPackage(AbstractFileProvider provider, GameFile file)
    {
        try
        {
            return (provider.LoadPackage(file), PackageLoadResult.Success);
        }
        catch (Exception e) when (e.Message.Contains("unversioned"))
        {
            return (null, PackageLoadResult.FailureRequiresUsmap);
        }
        catch
        {
            return (null, PackageLoadResult.FailureOther);
        }
    }
    
    private static UsmapRequirement GetUsmapRequirement(GameFile file)
    {
        var detectedNeedsUsmap = DetectNeedsUsmap(file);
        if (!detectedNeedsUsmap.HasValue)
            return UsmapRequirement.Unknown;

        return detectedNeedsUsmap.Value
            ? UsmapRequirement.RequiresUsmap
            : UsmapRequirement.DoesNotRequireUsmap;
    }

    private static UsmapRequirement GetUsmapRequirement(IPackage pkg)
        => pkg is AbstractUePackage absPkg && absPkg.HasFlags(EPackageFlags.PKG_UnversionedProperties)
            ? UsmapRequirement.RequiresUsmap
            : UsmapRequirement.DoesNotRequireUsmap;

    private static string GetLoadFailurePrefix(bool markUsmap, bool hasMappings, PackageLoadResult result)
    {
        if (!markUsmap)
            return "";

        return result switch
        {
            PackageLoadResult.FailureRequiresUsmap when hasMappings => "[!] ",
            PackageLoadResult.FailureRequiresUsmap => "[*] ",
            PackageLoadResult.FailureOther => "[ ] ",
            _ => throw new ArgumentOutOfRangeException(nameof(result), result, "Unsupported package load result")
        };
    }

    private static bool? DetectNeedsUsmap(GameFile file)
    {
        if (!file.TryCreateReader(out var ar)) return null;

        // Fast path for UE5 IoStore (Zen) packages: bHasVersioningInfo is the first uint32.
        // Zero means no versioning block -> unversioned -> needs usmap.
        if (file is FIoStoreEntry && ar.Game >= EGame.GAME_UE5_0)
            return ar.Read<uint>() == 0;

        // Fast path for modern UE4 PAK packages: parse the header up to PackageFlags.
        return TryReadPakPackageFlags(ar);
    }

    // Tries to read PackageFlags directly from the PAK package header without constructing a full Package object.
    // Returns null if the format is not one we can parse quickly.
    // Only handles modern UE4 PAK format: legacyFileVersion -6 or -7, which always uses the fixed-size
    // Optimized custom version serialization. Older formats (-5 and below use Guids/Enums with variable-length
    // entries) and UE5 PAK (where PACKAGE_SAVED_HASH inserts a SavedHash block before CustomVersionContainer)
    // are not handled here.
    private static bool? TryReadPakPackageFlags(FArchive ar)
    {
        const uint packageFileTag = 0x9E2A83C1U;
        const uint packageFileTagAe = 0x56DE5ECAU; // AshEchoes - normalised to standard

        try
        {
            var tag = ar.Read<uint>();
            if (tag == packageFileTagAe) tag = packageFileTag;
            if (tag != packageFileTag) return null;

            var legacyFileVersion = ar.Read<int>();

            // Only -6 and -7: modern UE4 Optimized-format custom versions, no PACKAGE_SAVED_HASH
            if (legacyFileVersion != -6 && legacyFileVersion != -7) return null;

            ar.Position += 4; // FileVersionUE3 (present for both -6 and -7, absent only for -4)
            ar.Position += 4; // FileVersionUE4
            ar.Position += 4; // FileVersionLicensee

            // Skip Optimized CustomVersionContainer: int32 count + count * FCustomVersion (FGuid 16 + int32 4 = 20 bytes)
            var cvCount = ar.Read<int>();
            ar.Position += cvCount * 20;

            ar.Position += 4; // TotalHeaderSize (read here because FileVersionUE < PACKAGE_SAVED_HASH)

            var strLen = ar.Read<int>();
            if (strLen > 0) ar.Position += strLen;
            else if (strLen < 0) ar.Position += -strLen * 2;

            var packageFlags = ar.Read<EPackageFlags>();
            return packageFlags.HasFlag(EPackageFlags.PKG_UnversionedProperties);
        }
        catch
        {
            return null;
        }
    }
}
