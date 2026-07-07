using System.IO;
using SkiaSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Svg.Skia;

namespace RonekaiImageFramer.Services;

public static class SvgRasterizer
{
    public const int DefaultMaxDimension = 2048;

    public static Image<Rgba32> Load(string filePath, int maxDimension = DefaultMaxDimension)
    {
        var fullPath = Path.GetFullPath(filePath);
        using var svg = new SKSvg();
        if (svg.Load(fullPath) is null || svg.Picture is null)
            throw new InvalidOperationException($"SVG okunamadı: {Path.GetFileName(fullPath)}");

        var bounds = svg.Picture.CullRect;
        float width = bounds.Width;
        float height = bounds.Height;

        if (width <= 0 || height <= 0)
        {
            width = 512;
            height = 512;
            bounds = SKRect.Create(0, 0, width, height);
        }

        int pixelW = Math.Max(1, (int)Math.Ceiling(width));
        int pixelH = Math.Max(1, (int)Math.Ceiling(height));
        float maxEdge = Math.Max(pixelW, pixelH);
        if (maxEdge > maxDimension)
        {
            float scale = maxDimension / maxEdge;
            pixelW = Math.Max(1, (int)Math.Ceiling(pixelW * scale));
            pixelH = Math.Max(1, (int)Math.Ceiling(pixelH * scale));
        }

        using var bitmap = new SKBitmap(pixelW, pixelH, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        float scaleX = pixelW / width;
        float scaleY = pixelH / height;
        canvas.Scale(scaleX, scaleY);
        if (bounds.Left != 0 || bounds.Top != 0)
            canvas.Translate(-bounds.Left, -bounds.Top);

        canvas.DrawPicture(svg.Picture);
        canvas.Flush();

        using var skImage = SKImage.FromBitmap(bitmap);
        using var encoded = skImage.Encode(SKEncodedImageFormat.Png, 100);
        if (encoded is null)
            throw new InvalidOperationException($"SVG rasterize edilemedi: {Path.GetFileName(fullPath)}");

        using var stream = new MemoryStream(encoded.ToArray());
        return Image.Load<Rgba32>(stream);
    }
}
