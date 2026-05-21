using RonekaiImageFramer.Models;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ImgColor = SixLabors.ImageSharp.Color;
using ImgRectangle = SixLabors.ImageSharp.Rectangle;

namespace RonekaiImageFramer.Services;

public static class BrandRenderer
{
    private static BrandColorTheme Theme => BrandThemeContext.Current;

    public static void DrawBrandHorizontal(
        Image<Rgba32> canvas,
        int x,
        int y,
        float ronekaiSize,
        ImgColor? primaryColor = null,
        ImgColor? accentColor = null,
        HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        var boldFamily = FontProvider.GetBoldFamily();
        var regularFamily = FontProvider.GetRegularFamily();

        var ronekaiFont = boldFamily.CreateFont(ronekaiSize, FontStyle.Bold);
        var denFont = regularFamily.CreateFont(ronekaiSize * 0.42f, FontStyle.Regular);

        var ronekaiText = ImageBrandContext.MainText;
        var denText = ImageBrandContext.SuffixText;
        var primary = primaryColor ?? Theme.RonekaiText;
        var accent = accentColor ?? Theme.DenText;

        var ronekaiSizeF = TextMeasurer.MeasureSize(ronekaiText, new TextOptions(ronekaiFont));
        var denSizeF = TextMeasurer.MeasureSize(denText, new TextOptions(denFont));

        float totalWidth = ronekaiSizeF.Width + denSizeF.Width;
        float startX = alignment switch
        {
            HorizontalAlignment.Center => x - totalWidth / 2f,
            HorizontalAlignment.Right => x - totalWidth,
            _ => x
        };

        float baselineY = y + ronekaiSizeF.Height * 0.82f;

        canvas.Mutate(ctx =>
        {
            ctx.DrawText(ronekaiText, ronekaiFont, primary, new PointF(startX, baselineY - ronekaiSizeF.Height));
            ctx.DrawText(denText, denFont, accent, new PointF(startX + ronekaiSizeF.Width, baselineY - denSizeF.Height + ronekaiSize * 0.08f));
        });
    }

    public static void DrawBrandOnBar(Image<Rgba32> canvas, ImgRectangle barBounds)
    {
        float fontSize = barBounds.Height * 0.38f;
        int centerX = barBounds.X + barBounds.Width / 2;
        int centerY = barBounds.Y + (int)(barBounds.Height * 0.18f);

        DrawBrandHorizontal(
            canvas, centerX, centerY, fontSize,
            Theme.RonekaiOnBar, Theme.DenOnBar,
            HorizontalAlignment.Center);
    }

    public static void DrawCornerWatermark(Image<Rgba32> canvas, int margin = 24)
    {
        float size = Math.Max(18, canvas.Width * 0.028f);
        int x = canvas.Width - margin;
        int y = canvas.Height - margin - (int)(size * 1.4f);

        canvas.Mutate(ctx =>
        {
            var bg = new RectangleF(x - size * 5.5f, y - size * 0.25f, size * 5.8f, size * 1.45f);
            var wash = Theme.Background.ToPixel<Rgba32>();
            ctx.Fill(ImgColor.FromRgba(wash.R, wash.G, wash.B, 220), bg);
        });

        DrawBrandHorizontal(canvas, x, y, size, Theme.RonekaiText, Theme.DenText, HorizontalAlignment.Right);
    }
}

public enum HorizontalAlignment
{
    Left,
    Center,
    Right
}
