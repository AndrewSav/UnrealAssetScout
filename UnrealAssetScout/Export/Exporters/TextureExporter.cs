using CUE4Parse_Conversion.Options;
using CUE4Parse_Conversion.Textures;
using CUE4Parse.UE4.Assets.Exports.Texture;
using UnrealAssetScout.Package;

namespace UnrealAssetScout.Export.Exporters;

// Exports Unreal texture assets to image files on disk.
// Called by TexturesPackageProcessor for every `UTexture` export. A texture that stores no image
// data has nothing to export and is declined rather than reported as a failure.
internal static class TextureExporter
{
    internal static ExportAttemptResult TryExport(UTexture texture, PackageExportContext packageContext, string outputDir, bool nestUnderPackage)
    {
        if (!HasImageData(texture))
            return ExportAttemptResult.NotHandled();

        var bitmap = texture.Decode();
        if (bitmap is null)
            return ExportAttemptResult.Failure($"{packageContext.Path}/{texture.Name}", "could not decode texture");

        var bytes = bitmap.Encode(ETextureFormat.Png, false, out var ext);
        var relativePath = ExportPathUtils.ComposeExportPath(
            packageContext.Path, ExportPathUtils.ComposeExportLeaf(texture), nestUnderPackage);
        var outPath = ExportPathUtils.ToOutputPath(outputDir, relativePath, $".{ext}");
        ExportPathUtils.WriteFile(outPath, bytes);
        return ExportAttemptResult.Success($"{packageContext.Path}/{texture.Name}", outPath);
    }

    // Render targets and media textures are drawn into while the game runs, so their cooked asset
    // stores settings but no pixels.
    private static bool HasImageData(UTexture texture) =>
        texture.PlatformData is { Mips.Length: > 0 } or { VTData: not null } or { CPUCopy: not null };
}
