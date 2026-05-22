using RonekaiImageFramer.Models;
using SixLabors.Fonts;
using SixLaborsFontFamily = SixLabors.Fonts.FontFamily;

namespace RonekaiImageFramer.Services;

public static class FontProvider
{
    private static readonly Dictionary<string, SixLaborsFontFamily> Cache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] BoldFallback =
    [
        @"C:\Windows\Fonts\segoeuib.ttf",
        @"C:\Windows\Fonts\arialbd.ttf",
    ];

    private static readonly string[] RegularFallback =
    [
        @"C:\Windows\Fonts\segoeui.ttf",
        @"C:\Windows\Fonts\arial.ttf",
    ];

    public static SixLaborsFontFamily GetBoldFamily(string? fontId = null) =>
        LoadFamily(BrandFontRegistry.GetById(fontId)?.BoldFontPath, BoldFallback);

    public static SixLaborsFontFamily GetRegularFamily(string? fontId = null) =>
        LoadFamily(BrandFontRegistry.GetById(fontId)?.RegularFontPath, RegularFallback);

    private static SixLaborsFontFamily LoadFamily(string? primaryPath, string[] fallbacks)
    {
        if (!string.IsNullOrEmpty(primaryPath) && TryLoadCached(primaryPath) is { } primary)
            return primary;

        foreach (var path in fallbacks)
        {
            if (TryLoadCached(path) is { } fallback)
                return fallback;
        }

        if (SystemFonts.Families.Any())
            return SystemFonts.Families.First();

        throw new InvalidOperationException("Sistem fontu bulunamadı. Windows font klasörünü kontrol edin.");
    }

    private static SixLaborsFontFamily? TryLoadCached(string path)
    {
        if (!File.Exists(path))
            return null;

        if (Cache.TryGetValue(path, out var cached))
            return cached;

        try
        {
            var family = new FontCollection().Add(path);
            Cache[path] = family;
            return family;
        }
        catch
        {
            return null;
        }
    }
}
