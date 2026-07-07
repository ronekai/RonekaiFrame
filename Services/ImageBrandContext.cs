using RonekaiImageFramer.Models;

namespace RonekaiImageFramer.Services;

public static class ImageBrandContext
{
    private static readonly AsyncLocal<ImageBrandSettings?> _current = new();

    public static ImageBrandSettings Current =>
        _current.Value ?? ImageBrandStore.Current;

    public static string MainText =>
        string.IsNullOrWhiteSpace(Current.MainText) ? "RONEKAI" : Current.MainText.Trim();

    public static string SuffixText => Current.SuffixText ?? "";

    public static bool ShowMainText => Current.ShowMainText;

    public static bool ShowSuffixText => Current.ShowSuffixText;

    public static float MainTextSizeScale => ScaleFromPercent(Current.MainTextSizePercent);

    public static float SuffixTextSizeScale => ScaleFromPercent(Current.SuffixTextSizePercent);

    public static bool ShouldDrawMain =>
        ShowMainText && !string.IsNullOrWhiteSpace(Current.MainText);

    public static bool ShouldDrawSuffix =>
        ShowSuffixText && !string.IsNullOrWhiteSpace(Current.SuffixText);

    public static bool HasVisibleBrand => ShouldDrawMain || ShouldDrawSuffix;

    public static bool ShowBrandLogo => Current.ShowBrandLogo;

    public static string? BrandLogoPath => Current.BrandLogoPath;

    public static float BrandLogoOpacity => Math.Clamp(Current.BrandLogoOpacity, 0.05f, 1f);

    public static float BrandLogoSizeScale => Math.Clamp(Current.BrandLogoSizePercent, 25, 300) / 100f;

    public static OverlayPlacement BrandLogoPlacement => Current.BrandLogoPlacement;

    public static int BrandLogoOffsetX => Current.BrandLogoOffsetX;

    public static int BrandLogoOffsetY => Current.BrandLogoOffsetY;

    public static bool BrandLogoTintEnabled => Current.BrandLogoTintEnabled;

    public static ThemeColorAppearance BrandLogoTint => Current.BrandLogoTint;

    public static bool ShouldDrawBrandLogo =>
        ShowBrandLogo && !string.IsNullOrWhiteSpace(BrandLogoPath) && File.Exists(BrandLogoPath!);

    public static string MainFontId =>
        string.IsNullOrWhiteSpace(Current.MainFontId) ? "segoe-ui" : Current.MainFontId.Trim();

    public static string SuffixFontId =>
        string.IsNullOrWhiteSpace(Current.SuffixFontId) ? "segoe-ui" : Current.SuffixFontId.Trim();

    private static float ScaleFromPercent(int percent) =>
        Math.Clamp(percent, 25, 300) / 100f;

    public static IDisposable Use(ImageBrandSettings settings)
    {
        var previous = _current.Value;
        _current.Value = settings;
        return new Scope(previous);
    }

    private sealed class Scope(ImageBrandSettings? previous) : IDisposable
    {
        public void Dispose() => _current.Value = previous;
    }
}
