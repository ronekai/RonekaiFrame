using SixLabors.ImageSharp.PixelFormats;
using ImgColor = SixLabors.ImageSharp.Color;

namespace RonekaiImageFramer.Models;

public sealed record BrandColorTheme(
    string Id,
    string Name,
    string BackgroundHex,
    string RonekaiHex,
    string DenHex,
    bool IsCustom = false)
{
    public ImgColor Background => ParseColor(BackgroundHex);
    public ImgColor RonekaiText => ParseColor(RonekaiHex);
    public ImgColor DenText => ParseColor(DenHex);

    public ImgColor ProductSurface => IsLight(Background) ? ImgColor.White : Lighten(Background, 0.12f);
    public ImgColor BarBackground => RonekaiText;
    public ImgColor AccentLine => DenText;
    public ImgColor FrameBorder => RonekaiText;
    public ImgColor InnerPanel => IsLight(Background)
        ? Lighten(Background, 0.04f)
        : Darken(Background, 0.08f);

    public ImgColor RonekaiOnBar => ContrastText(BarBackground, RonekaiText, ImgColor.White);
    public ImgColor DenOnBar => DenText;

    public static BrandColorTheme CreateCustom(string backgroundHex, string ronekaiHex, string denHex) =>
        new("ozel", "Özel (kendin seç)", backgroundHex, ronekaiHex, denHex, IsCustom: true);

    private static ImgColor ParseColor(string hex) => ImgColor.ParseHex(NormalizeHex(hex));

    private static string NormalizeHex(string hex)
    {
        hex = hex.Trim();
        if (!hex.StartsWith('#')) hex = "#" + hex;
        return hex.Length is 7 or 9 ? hex : "#F5F6F8";
    }

    private static bool IsLight(ImgColor c) => Luminance(c) > 0.62f;

    private static bool IsDark(ImgColor c) => Luminance(c) < 0.35f;

    private static Rgba32 ToRgba(ImgColor c) => c.ToPixel<Rgba32>();

    private static float Luminance(ImgColor c)
    {
        var p = ToRgba(c);
        return (0.299f * p.R + 0.587f * p.G + 0.114f * p.B) / 255f;
    }

    private static ImgColor ContrastText(ImgColor background, ImgColor preferred, ImgColor fallback) =>
        IsDark(background) ? fallback : preferred;

    private static ImgColor Lighten(ImgColor c, float amount)
    {
        var p = ToRgba(c);
        byte L(byte ch) => (byte)Math.Min(255, ch + (255 - ch) * amount);
        return ImgColor.FromRgb(L(p.R), L(p.G), L(p.B));
    }

    private static ImgColor Darken(ImgColor c, float amount)
    {
        var p = ToRgba(c);
        byte D(byte ch) => (byte)Math.Max(0, ch * (1f - amount));
        return ImgColor.FromRgb(D(p.R), D(p.G), D(p.B));
    }
}
