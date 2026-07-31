using RonekaiImageFramer.Models;

namespace RonekaiImageFramer.Services;

public static class BrandLogoResolver
{
    /// <param name="preferPerFileOverrides">
    /// true: dosya-başı override varsa onu kullan (seçili dosya işle modu).
    /// false: yalnızca klasör varsayılanı — tüm klasör aynı logo/konum (checkbox kapalı).
    /// </param>
    public static ImageBrandSettings ResolveForFile(
        string filePath,
        ImageBrandSettings global,
        SourceFolderLogoSettings? folderSettings,
        bool preferPerFileOverrides = true)
    {
        if (folderSettings is null)
            return global.Clone();

        FileBrandLogoOverride? o = null;

        if (preferPerFileOverrides)
        {
            var key = NormalizePath(filePath);
            if (folderSettings.PerFile.TryGetValue(key, out var perFile))
                o = perFile;
        }

        o ??= folderSettings.FolderDefault;

        if (o is null)
            return global.Clone();

        var result = global.Clone();
        ApplyOverride(result, o);
        return result;
    }

    public static void ApplyOverride(ImageBrandSettings target, FileBrandLogoOverride o)
    {
        target.ShowBrandLogo = o.Enabled;
        target.BrandLogoPath = BrandLogoCatalog.ResolvePath(o.LogoPresetId, o.LogoPath)
                               ?? o.LogoPath
                               ?? target.BrandLogoPath;
        target.BrandLogoPresetId = o.LogoPresetId ?? BrandLogoCatalog.DetectPresetId(target.BrandLogoPath);
        target.BrandLogoSizePercent = o.SizePercent;
        target.BrandLogoOpacity = o.Opacity;
        target.BrandLogoPlacement = ParsePlacement(o.PlacementId, target.BrandLogoPlacement);
        target.BrandLogoOffsetX = o.OffsetX;
        target.BrandLogoOffsetY = o.OffsetY;
        target.BrandLogoTintEnabled = o.BrandLogoTintEnabled;
        target.BrandLogoTint = o.BrandLogoTint.Clone();
    }

    public static FileBrandLogoOverride CreateOverrideFromGlobal(ImageBrandSettings global) => new()
    {
        Enabled = global.ShowBrandLogo,
        LogoPresetId = global.BrandLogoPresetId ?? BrandLogoCatalog.DetectPresetId(global.BrandLogoPath),
        LogoPath = global.BrandLogoPath,
        SizePercent = global.BrandLogoSizePercent,
        Opacity = global.BrandLogoOpacity,
        PlacementId = global.BrandLogoPlacement.ToString(),
        OffsetX = global.BrandLogoOffsetX,
        OffsetY = global.BrandLogoOffsetY,
        BrandLogoTintEnabled = global.BrandLogoTintEnabled,
        BrandLogoTint = global.BrandLogoTint.Clone()
    };

    public static FileBrandLogoOverride CreateOverrideFromSettings(ImageBrandSettings settings) =>
        CreateOverrideFromGlobal(settings);

    public static string NormalizePath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static OverlayPlacement ParsePlacement(string? id, OverlayPlacement fallback) =>
        Enum.TryParse<OverlayPlacement>(id, ignoreCase: true, out var p) ? p : fallback;
}