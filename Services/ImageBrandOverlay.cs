using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace RonekaiImageFramer.Services;

/// <summary>Şablon uygulanmadan (yalnızca boyutlandır / şablon yok) görsele marka ekler.</summary>
public static class ImageBrandOverlay
{
    public static bool ShouldApply =>
        ImageBrandContext.ShouldDrawBrandLogo || ImageBrandContext.HasVisibleBrand;

    public static void ApplyBrandLogo(Image<Rgba32> canvas)
    {
        if (!ImageBrandContext.ShouldDrawBrandLogo)
            return;

        int margin = Math.Max(16, (int)(Math.Min(canvas.Width, canvas.Height) * 0.03f));
        BrandLogoRenderer.DrawOnCanvas(canvas, margin);
    }

    public static void ApplyToCanvas(Image<Rgba32> canvas)
    {
        if (!ShouldApply)
            return;

        int margin = Math.Max(16, (int)(Math.Min(canvas.Width, canvas.Height) * 0.03f));
        LogoPlacementContext.Reset();
        ApplyBrandLogo(canvas);
        if (ImageBrandContext.HasVisibleBrand)
            BrandRenderer.DrawCornerWatermark(canvas, margin);
    }
}
