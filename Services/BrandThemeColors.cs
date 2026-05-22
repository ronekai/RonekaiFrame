using RonekaiImageFramer.Models;
using SixLabors.ImageSharp.PixelFormats;
using ImgColor = SixLabors.ImageSharp.Color;

namespace RonekaiImageFramer.Services;

/// <summary>Geçerli tema + opaklık/gradyan ile çözümlenen renkler.</summary>
public static class BrandThemeColors
{
    public static ImgColor Background => ThemeFillRenderer.RepresentativeColor(ThemeColorSlot.Background);
    public static ImgColor RonekaiText => ThemeFillRenderer.RepresentativeColor(ThemeColorSlot.MainText);
    public static ImgColor DenText => ThemeFillRenderer.RepresentativeColor(ThemeColorSlot.Suffix);
    public static ImgColor ProductSurface => Background;

    public static ImgColor BarBackground => RonekaiText;
    public static ImgColor AccentLine => DenText;
    public static ImgColor FrameBorder => RonekaiText;

    public static ImgColor InnerPanel => IsLight(Background)
        ? Lighten(Background, 0.04f)
        : Darken(Background, 0.08f);

    public static ImgColor RonekaiOnBackground => ReadableOn(Background, RonekaiText);
    public static ImgColor DenOnBackground => ReadableOn(Background, DenText);
    public static ImgColor RonekaiOnBar => ReadableOn(BarBackground, ImgColor.White);
    public static ImgColor DenOnBar => ReadableOn(BarBackground, DenText);

    private static bool IsLight(ImgColor c) => Luminance(c) > 0.62f;
    private static bool IsDark(ImgColor c) => Luminance(c) < 0.35f;

    private static float Luminance(ImgColor c)
    {
        var p = c.ToPixel<Rgba32>();
        return (0.299f * p.R + 0.587f * p.G + 0.114f * p.B) / 255f;
    }

    private static ImgColor ReadableOn(ImgColor background, ImgColor preferred) =>
        Math.Abs(Luminance(background) - Luminance(preferred)) >= 0.28f
            ? preferred
            : IsDark(background) ? ImgColor.White : ImgColor.Black;

    private static ImgColor Lighten(ImgColor c, float amount)
    {
        var p = c.ToPixel<Rgba32>();
        byte L(byte ch) => (byte)Math.Min(255, ch + (255 - ch) * amount);
        return ImgColor.FromRgb(L(p.R), L(p.G), L(p.B));
    }

    private static ImgColor Darken(ImgColor c, float amount)
    {
        var p = c.ToPixel<Rgba32>();
        byte D(byte ch) => (byte)Math.Max(0, ch * (1f - amount));
        return ImgColor.FromRgb(D(p.R), D(p.G), D(p.B));
    }
}
