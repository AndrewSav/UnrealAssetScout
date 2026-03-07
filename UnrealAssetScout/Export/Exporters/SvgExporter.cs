using System;
using System.IO;
using CUE4Parse.GameTypes.KRD.Assets.Exports;
using UnrealAssetScout.Package;
using SkiaSharp;
using Svg.Skia;

namespace UnrealAssetScout.Export.Exporters;

// Exports `USvgAsset` payloads as rendered PNG files.
// Called by ExportProcessor graphics-mode handlers when an SVG asset should be rasterized into the
// target output directory.
internal static class SvgExporter
{
    internal static ExportAttemptResult TryExport(USvgAsset svgAsset, PackageExportContext packageContext, string outputDir)
    {
        var svgData = svgAsset.GetOrDefault<byte[]>("SvgData");
        if (svgData is not { Length: > 0 })
            return ExportAttemptResult.Failure($"{packageContext.Path}/{svgAsset.Name}", "missing SvgData");

        using var stream = new MemoryStream(svgData);
        stream.Position = 0;
        var svg = new SKSvg();
        svg.Load(stream);
        if (svg.Picture is null)
            return ExportAttemptResult.Failure($"{packageContext.Path}/{svgAsset.Name}", "could not load SVG");

        const int size = 512;
        var bounds = svg.Picture.CullRect;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return ExportAttemptResult.Failure($"{packageContext.Path}/{svgAsset.Name}", "invalid SVG bounds");

        float scale = Math.Min(size / bounds.Width, size / bounds.Height);
        using var bitmap = new SKBitmap(size, size);
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint();
        paint.IsAntialias = true;
        paint.FilterQuality = SKFilterQuality.Medium;
        canvas.Clear(SKColors.Transparent);
        canvas.Scale(scale);
        canvas.Translate(-bounds.Left, -bounds.Top);
        canvas.DrawPicture(svg.Picture, paint);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        if (encoded is null)
            return ExportAttemptResult.Failure($"{packageContext.Path}/{svgAsset.Name}", "could not encode rendered SVG");

        var dir = ExportPathUtils.GetPackageDirectory(packageContext.Path);
        var outPath = ExportPathUtils.ToOutputPath(outputDir, $"{dir}/{svgAsset.Name}", ".png");
        ExportPathUtils.WriteFile(outPath, encoded.ToArray());
        return ExportAttemptResult.Success($"{packageContext.Path}/{svgAsset.Name}", outPath);
    }
}
