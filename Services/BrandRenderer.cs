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
    private sealed record BrandLayout(
        bool DrawMain,
        bool DrawSuffix,
        string MainText,
        string SuffixText,
        Font? MainFont,
        Font? SuffixFont,
        SizeF MainSize,
        SizeF SuffixSize,
        float TotalWidth,
        float BlockHeight);

    public static void DrawBrandHorizontal(
        Image<Rgba32> canvas,
        int x,
        int y,
        float ronekaiSize,
        ImgColor? primaryColor = null,
        ImgColor? accentColor = null,
        HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        if (!TryCreateLayout(ronekaiSize, out var layout))
            return;

        try
        {
            DrawLayout(canvas, layout, x, y, primaryColor, accentColor, alignment);
        }
        catch
        {
            try
            {
                DrawLayout(canvas, layout, x, y,
                    primaryColor ?? BrandThemeColors.RonekaiText,
                    accentColor ?? BrandThemeColors.DenText,
                    alignment);
            }
            catch
            {
            }
        }
    }

    public static void DrawBrandOnBar(Image<Rgba32> canvas, ImgRectangle barBounds)
    {
        if (!ImageBrandContext.HasVisibleBrand)
            return;

        float fontSize = barBounds.Height * 0.38f;
        int centerX = barBounds.X + barBounds.Width / 2;
        int centerY = barBounds.Y + (int)(barBounds.Height * 0.18f);

        DrawBrandHorizontal(
            canvas, centerX, centerY, fontSize,
            BrandThemeColors.RonekaiOnBar, BrandThemeColors.DenOnBar,
            HorizontalAlignment.Center);
    }

    public static void DrawCornerWatermark(Image<Rgba32> canvas, int margin = 24)
    {
        if (!ImageBrandContext.HasVisibleBrand)
            return;

        float baseSize = Math.Max(18, canvas.Width * 0.028f);
        if (!TryCreateLayout(baseSize, out var layout))
            return;

        int x = canvas.Width - margin;
        int y = canvas.Height - margin - (int)(layout.BlockHeight * 1.15f);
        float padX = baseSize * 0.35f;
        float padY = baseSize * 0.25f;

        float startX = x - layout.TotalWidth;
        var bg = new RectangleF(
            startX - padX,
            y - padY,
            layout.TotalWidth + padX * 2f,
            layout.BlockHeight + padY * 2f);

        canvas.Mutate(ctx =>
        {
            var wash = BrandThemeColors.Background.ToPixel<Rgba32>();
            ctx.Fill(ImgColor.FromRgba(wash.R, wash.G, wash.B, 220), bg);
        });

        DrawBrandHorizontal(canvas, x, y, baseSize, null, null, HorizontalAlignment.Right);
    }

    private static bool TryCreateLayout(float baseSize, out BrandLayout layout)
    {
        layout = default!;
        bool drawMain = ImageBrandContext.ShouldDrawMain;
        bool drawSuffix = ImageBrandContext.ShouldDrawSuffix;
        if (!drawMain && !drawSuffix)
            return false;

        try
        {
            var boldFamily = FontProvider.GetBoldFamily(ImageBrandContext.MainFontId);
            var regularFamily = FontProvider.GetRegularFamily(ImageBrandContext.SuffixFontId);

            float mainFontSize = baseSize * ImageBrandContext.MainTextSizeScale;
            float suffixFontSize = baseSize * 0.42f * ImageBrandContext.SuffixTextSizeScale;

            Font? mainFont = null;
            Font? suffixFont = null;
            SizeF mainSize = SizeF.Empty;
            SizeF suffixSize = SizeF.Empty;
            string mainText = "";
            string suffixText = "";

            if (drawMain)
            {
                mainText = ImageBrandContext.MainText;
                mainFont = boldFamily.CreateFont(mainFontSize, FontStyle.Bold);
                var mainMeasured = TextMeasurer.MeasureSize(mainText, new TextOptions(mainFont));
                mainSize = new SizeF(mainMeasured.Width, mainMeasured.Height);
            }

            if (drawSuffix)
            {
                suffixText = ImageBrandContext.SuffixText;
                suffixFont = regularFamily.CreateFont(suffixFontSize, FontStyle.Regular);
                var suffixMeasured = TextMeasurer.MeasureSize(suffixText, new TextOptions(suffixFont));
                suffixSize = new SizeF(suffixMeasured.Width, suffixMeasured.Height);
            }

            float totalWidth = (drawMain ? mainSize.Width : 0f) + (drawSuffix ? suffixSize.Width : 0f);
            float blockHeight = Math.Max(drawMain ? mainSize.Height : 0f, drawSuffix ? suffixSize.Height : 0f);

            layout = new BrandLayout(
                drawMain, drawSuffix, mainText, suffixText,
                mainFont, suffixFont, mainSize, suffixSize, totalWidth, blockHeight);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void DrawLayout(
        Image<Rgba32> canvas,
        BrandLayout layout,
        int x,
        int y,
        ImgColor? primaryColor,
        ImgColor? accentColor,
        HorizontalAlignment alignment)
    {
        float startX = alignment switch
        {
            HorizontalAlignment.Center => x - layout.TotalWidth / 2f,
            HorizontalAlignment.Right => x - layout.TotalWidth,
            _ => x
        };

        float baselineY = y + layout.BlockHeight * 0.82f;
        var brushBounds = new RectangleF(startX, baselineY - layout.BlockHeight, layout.TotalWidth, layout.BlockHeight);

        canvas.Mutate(ctx =>
        {
            float cursorX = startX;

            if (layout.DrawMain && layout.MainFont is not null)
            {
                var mainPoint = new PointF(cursorX, baselineY - layout.MainSize.Height);
                if (primaryColor is { } solidMain)
                    ctx.DrawText(layout.MainText, layout.MainFont, solidMain, mainPoint);
                else
                    ctx.DrawText(layout.MainText, layout.MainFont,
                        ThemeFillRenderer.CreateTextBrush(brushBounds, ThemeColorSlot.MainText), mainPoint);
                cursorX += layout.MainSize.Width;
            }

            if (layout.DrawSuffix && layout.SuffixFont is not null)
            {
                float suffixY = layout.DrawMain
                    ? baselineY - layout.SuffixSize.Height + layout.MainSize.Height * 0.08f
                    : baselineY - layout.SuffixSize.Height;
                var suffixPoint = new PointF(cursorX, suffixY);
                if (accentColor is { } solidAccent)
                    ctx.DrawText(layout.SuffixText, layout.SuffixFont, solidAccent, suffixPoint);
                else
                    ctx.DrawText(layout.SuffixText, layout.SuffixFont,
                        ThemeFillRenderer.CreateTextBrush(brushBounds, ThemeColorSlot.Suffix), suffixPoint);
            }
        });
    }
}

public enum HorizontalAlignment
{
    Left,
    Center,
    Right
}
