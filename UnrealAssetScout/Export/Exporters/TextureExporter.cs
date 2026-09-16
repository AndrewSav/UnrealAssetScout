using CUE4Parse_Conversion.Options;
using CUE4Parse_Conversion.Textures;
using CUE4Parse.UE4.Assets.Exports.Texture;
using UnrealAssetScout.Package;

namespace UnrealAssetScout.Export.Exporters;

// Exports Unreal texture assets to image files on disk.
// Called by ExportProcessor textures-mode handlers when a supported `UTexture` needs to be decoded
// and written to the output directory.
internal static class TextureExporter
{
    internal static ExportAttemptResult TryExport(UTexture texture, PackageExportContext packageContext, string outputDir, bool nestUnderPackage)
    {
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
}
