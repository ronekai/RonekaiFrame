using RonekaiImageFramer.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;

namespace RonekaiImageFramer.Services;

public static class ImageCropper
{
    /// <summary>
    /// Normalize edilmiş kırpma dikdörtgenini görselin mevcut piksel boyutlarına dönüştürür,
    /// sonra gerçekten görseli kırpar (boyut küçülür).
    /// </summary>
    public static void ApplyNormalizedCrop(Image<Rgba32> image, NormalizedCropRect crop)
    {
        if (crop.Width <= 0 || crop.Height <= 0)
            return;

        // Normalize edilmiş değerleri 0..1 aralığına zorla.
        double left = Math.Clamp(crop.Left, 0, 1);
        double top = Math.Clamp(crop.Top, 0, 1);
        double right = Math.Clamp(crop.Left + crop.Width, 0, 1);
        double bottom = Math.Clamp(crop.Top + crop.Height, 0, 1);

        if (right - left < 0.002 || bottom - top < 0.002)
            return;

        int w = image.Width;
        int h = image.Height;

        int x = (int)Math.Round(left * w);
        int y = (int)Math.Round(top * h);
        int cw = (int)Math.Round((right - left) * w);
        int ch = (int)Math.Round((bottom - top) * h);

        // Güvenlik: kenarlar taşmasın ve en az 1 piksel olsun.
        x = Math.Clamp(x, 0, w - 1);
        y = Math.Clamp(y, 0, h - 1);
        cw = Math.Clamp(cw, 1, w - x);
        ch = Math.Clamp(ch, 1, h - y);

        image.Mutate(ctx => ctx.Crop(new Rectangle(x, y, cw, ch)));
    }
}

