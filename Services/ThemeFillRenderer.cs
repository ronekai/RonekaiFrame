using RonekaiImageFramer.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ImgColor = SixLabors.ImageSharp.Color;
using ImgRectangle = SixLabors.ImageSharp.Rectangle;

namespace RonekaiImageFramer.Services;

public static class ThemeFillRenderer
{
    public static ThemeColorAppearance GetAppearance(ThemeColorSlot slot) =>
        BrandThemeContext.Appearance.Get(slot);

    public static ImgColor RepresentativeColor(ThemeColorSlot slot)
    {
        var a = GetAppearance(slot);
        if (a.FillMode == ColorFillMode.Gradient)
        {
            var c1 = ToColor(a.PrimaryHex, a.Opacity);
            var c2 = ToColor(a.GradientEndHex, a.Opacity);
            var p1 = c1.ToPixel<Rgba32>();
            var p2 = c2.ToPixel<Rgba32>();
            return ImgColor.FromRgba(
                (byte)((p1.R + p2.R) / 2),
                (byte)((p1.G + p2.G) / 2),
                (byte)((p1.B + p2.B) / 2),
                (byte)((p1.A + p2.A) / 2));
        }

        return ToColor(a.PrimaryHex, a.Opacity);
    }

    public static void Fill(Image<Rgba32> image, RectangleF rect, ThemeColorSlot slot) =>
        image.Mutate(ctx => Fill(ctx, rect, slot));

    public static void Fill(IImageProcessingContext ctx, ImgRectangle rect, ThemeColorSlot slot) =>
        Fill(ctx, new RectangleF(rect.X, rect.Y, rect.Width, rect.Height), slot);

    public static void Fill(IImageProcessingContext ctx, RectangleF rect, ThemeColorSlot slot)
    {
        var appearance = GetAppearance(slot);
        if (appearance.FillMode == ColorFillMode.Gradient)
        {
            var brush = CreateGradientBrush(rect, appearance);
            ctx.Fill(brush, rect);
            return;
        }

        ctx.Fill(ToColor(appearance.PrimaryHex, appearance.Opacity), rect);
    }

    public static void Fill(IImageProcessingContext ctx, IPath path, ThemeColorSlot slot)
    {
        var appearance = GetAppearance(slot);
        if (appearance.FillMode == ColorFillMode.Gradient)
        {
            var brush = CreateGradientBrush(path.Bounds, appearance);
            ctx.Fill(brush, path);
            return;
        }

        ctx.Fill(ToColor(appearance.PrimaryHex, appearance.Opacity), path);
    }

    public static Brush CreateTextBrush(RectangleF textBounds, ThemeColorSlot slot)
    {
        var appearance = GetAppearance(slot);
        if (appearance.FillMode == ColorFillMode.Solid)
            return new SolidBrush(ToColor(appearance.PrimaryHex, appearance.Opacity));

        return CreateGradientBrush(textBounds, appearance);
    }

    private static LinearGradientBrush CreateGradientBrush(RectangleF rect, ThemeColorAppearance appearance)
    {
        var (start, end) = GetGradientPoints(rect, appearance.GradientDirection);
        return new LinearGradientBrush(
            start,
            end,
            GradientRepetitionMode.None,
            new ColorStop(0f, ToColor(appearance.PrimaryHex, appearance.Opacity)),
            new ColorStop(1f, ToColor(appearance.GradientEndHex, appearance.Opacity)));
    }

    private static (PointF Start, PointF End) GetGradientPoints(RectangleF rect, GradientDirection direction) =>
        direction switch
        {
            GradientDirection.Horizontal => (new PointF(rect.Left, rect.Top), new PointF(rect.Right, rect.Top)),
            GradientDirection.DiagonalDown => (new PointF(rect.Left, rect.Top), new PointF(rect.Right, rect.Bottom)),
            GradientDirection.DiagonalUp => (new PointF(rect.Left, rect.Bottom), new PointF(rect.Right, rect.Top)),
            _ => (new PointF(rect.Left, rect.Top), new PointF(rect.Left, rect.Bottom))
        };

    private static ImgColor ToColor(string hex, float opacity)
    {
        hex = NormalizeHex(hex);
        var c = ImgColor.ParseHex(hex).ToPixel<Rgba32>();
        byte a = (byte)Math.Clamp((int)(c.A * Math.Clamp(opacity, 0f, 1f)), 0, 255);
        return ImgColor.FromRgba(c.R, c.G, c.B, a);
    }

    private static string NormalizeHex(string hex)
    {
        hex = hex.Trim();
        if (!hex.StartsWith('#')) hex = "#" + hex;
        return hex.Length is 7 or 9 ? hex : "#F5F6F8";
    }
}
