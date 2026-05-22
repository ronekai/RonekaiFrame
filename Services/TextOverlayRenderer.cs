using RonekaiImageFramer.Models;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ImgColor = SixLabors.ImageSharp.Color;

namespace RonekaiImageFramer.Services;

public static class TextOverlayRenderer
{
    public static Image<Rgba32> Apply(Image<Rgba32> canvas, TextOverlaySettings settings, BrandColorTheme theme)
    {
        if (!settings.HasText)
            return canvas.CloneAs<Rgba32>();

        var result = canvas.CloneAs<Rgba32>();
        var font = FontProvider.GetBoldFamily().CreateFont(Math.Max(14, canvas.Width * 0.028f), FontStyle.Bold);
        var text = settings.Text.Trim();
        var size = TextMeasurer.MeasureSize(text, new TextOptions(font));

        float x = settings.Position switch
        {
            TextOverlayPosition.BottomLeft => canvas.Width * 0.04f,
            TextOverlayPosition.TopCenter => (canvas.Width - size.Width) / 2f,
            _ => (canvas.Width - size.Width) / 2f
        };

        float y = settings.Position switch
        {
            TextOverlayPosition.TopCenter => canvas.Height * 0.04f,
            _ => canvas.Height - size.Height - canvas.Height * 0.04f
        };

        var wash = new RectangleF(x - 8, y - 6, size.Width + 16, size.Height + 12);
        byte alpha = (byte)(settings.Opacity * 255);
        var textColor = BrandThemeColors.RonekaiText.ToPixel<Rgba32>();
        var bg = BrandThemeColors.Background.ToPixel<Rgba32>();

        result.Mutate(ctx =>
        {
            ctx.Fill(ImgColor.FromRgba(bg.R, bg.G, bg.B, (byte)(alpha * 0.75f)), wash);
            ctx.DrawText(text, font, ImgColor.FromRgba(textColor.R, textColor.G, textColor.B, alpha), new PointF(x, y));
        });

        return result;
    }
}
