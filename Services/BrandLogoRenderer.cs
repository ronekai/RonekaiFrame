using RonekaiImageFramer.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ImgSize = SixLabors.ImageSharp.Size;
using ImgPoint = SixLabors.ImageSharp.Point;

namespace RonekaiImageFramer.Services;

public static class BrandLogoRenderer
{
    private static readonly object CacheGate = new();
    private static string? _cachePath;
    private static Image<Rgba32>? _cacheImage;

    public static void ClearCache()
    {
        lock (CacheGate)
        {
            _cacheImage?.Dispose();
            _cacheImage = null;
            _cachePath = null;
        }
    }

    public static bool ShouldDraw => ImageBrandContext.ShouldDrawBrandLogo;

    public static void DrawOnCanvas(Image<Rgba32> canvas, int margin = 24)
    {
        if (!ShouldDraw)
            return;

        using var logo = LoadBrandLogo();
        if (logo is null)
            return;

        float opacity = ImageBrandContext.BrandLogoOpacity;
        var placement = ImageBrandContext.BrandLogoPlacement;
        float scale = ImageBrandContext.BrandLogoSizeScale;

        if (placement == OverlayPlacement.Diagonal)
        {
            DrawDiagonal(canvas, logo, opacity, scale);
            return;
        }

        int target = Math.Max(32, (int)(Math.Min(canvas.Width, canvas.Height) * 0.18f * scale));
        using var resized = ResizeLogo(logo, target);
        using var prepared = PrepareForDraw(resized);
        var point = ApplyOffset(OverlayPlacementHelper.GetTopLeft(placement, canvas.Size, prepared.Size, margin));
        canvas.Mutate(ctx => ctx.DrawImage(prepared, point, opacity));
    }

    private static void DrawDiagonal(Image<Rgba32> canvas, Image<Rgba32> logo, float opacity, float scale)
    {
        int target = Math.Max(48, (int)(Math.Max(canvas.Width, canvas.Height) * 0.42f * scale));
        using var resized = ResizeLogo(logo, target);
        using var rotated = resized.Clone(ctx => ctx.Rotate(-32));
        using var prepared = PrepareForDraw(rotated);
        var point = ApplyOffset(OverlayPlacementHelper.GetTopLeft(OverlayPlacement.Center, canvas.Size, prepared.Size, 0));
        canvas.Mutate(ctx => ctx.DrawImage(prepared, point, opacity));
    }

    private static Point ApplyOffset(Point point) =>
        new(
            point.X + ImageBrandContext.BrandLogoOffsetX,
            point.Y + ImageBrandContext.BrandLogoOffsetY);

    private static Image<Rgba32> PrepareForDraw(Image<Rgba32> logo) =>
        ImageBrandContext.BrandLogoTintEnabled
            ? LogoTintRenderer.Apply(logo, ImageBrandContext.BrandLogoTint)
            : logo.CloneAs<Rgba32>();

    private static Image<Rgba32>? LoadBrandLogo()
    {
        string? path = ImageBrandContext.BrandLogoPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        path = Path.GetFullPath(path);
        lock (CacheGate)
        {
            if (_cacheImage is not null && string.Equals(_cachePath, path, StringComparison.OrdinalIgnoreCase))
                return _cacheImage.CloneAs<Rgba32>();

            _cacheImage?.Dispose();
            using var loaded = LogoImageLoader.Load(path);
            _cachePath = path;
            _cacheImage = loaded.CloneImage();
            return _cacheImage.CloneAs<Rgba32>();
        }
    }

    private static Image<Rgba32> ResizeLogo(Image<Rgba32> source, int maxEdge) =>
        source.Clone(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new ImgSize(maxEdge, maxEdge),
            Mode = ResizeMode.Max,
            Sampler = KnownResamplers.Lanczos3
        }));
}
