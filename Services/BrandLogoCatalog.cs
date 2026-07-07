namespace RonekaiImageFramer.Services;

/// <summary>PhonixFrame ile gelen varsayılan marka logoları (beyaz / siyah filigran).</summary>
public static class BrandLogoCatalog
{
    public const string WhitePresetId = "white";
    public const string BlackPresetId = "black";
    public const string HorizontalWhitePresetId = "hwhite";
    public const string HorizontalBlackPresetId = "hblack";

    public const string WhiteFileName = "filigram-08.svg";
    public const string BlackFileName = "filigram-09.svg";
    public const string HorizontalWhiteFileName = "nadir-figur-yatay-beyaz.svg";
    public const string HorizontalBlackFileName = "nadir-figur-yatay-siyah.svg";

    public static string AssetsFolder => Path.Combine(AppPaths.ProgramRoot, "Assets");

    public static string WhiteLogoPath => Path.Combine(AssetsFolder, WhiteFileName);
    public static string BlackLogoPath => Path.Combine(AssetsFolder, BlackFileName);
    public static string HorizontalWhiteLogoPath => Path.Combine(AssetsFolder, HorizontalWhiteFileName);
    public static string HorizontalBlackLogoPath => Path.Combine(AssetsFolder, HorizontalBlackFileName);

    public static IReadOnlyList<BrandLogoPreset> Presets { get; } =
    [
        new(WhitePresetId, "Beyaz logo", WhiteFileName),
        new(BlackPresetId, "Siyah logo", BlackFileName),
        new(HorizontalWhitePresetId, "Yatay beyaz logo", HorizontalWhiteFileName),
        new(HorizontalBlackPresetId, "Yatay siyah logo", HorizontalBlackFileName),
    ];

    public static void EnsureBundledLogos()
    {
        Directory.CreateDirectory(AssetsFolder);

        EnsureLogoFile(WhiteLogoPath, WhiteFileName);
        EnsureLogoFile(BlackLogoPath, BlackFileName);
    }

    private static void EnsureLogoFile(string targetPath, string fileName)
    {
        if (File.Exists(targetPath))
            return;

        var downloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");

        foreach (var candidate in new[]
                 {
                     Path.Combine(downloads, fileName),
                     Path.Combine(downloads, fileName.Replace(".svg", " (1).svg", StringComparison.Ordinal))
                 })
        {
            if (!File.Exists(candidate))
                continue;

            try
            {
                File.Copy(candidate, targetPath, overwrite: false);
                return;
            }
            catch
            {
                // başka kaynak dene
            }
        }
    }

    public static string? ResolvePath(string? presetId, string? customPath)
    {
        if (string.Equals(presetId, WhitePresetId, StringComparison.OrdinalIgnoreCase)
            && File.Exists(WhiteLogoPath))
            return WhiteLogoPath;

        if (string.Equals(presetId, BlackPresetId, StringComparison.OrdinalIgnoreCase)
            && File.Exists(BlackLogoPath))
            return BlackLogoPath;

        if (string.Equals(presetId, HorizontalWhitePresetId, StringComparison.OrdinalIgnoreCase)
            && File.Exists(HorizontalWhiteLogoPath))
            return HorizontalWhiteLogoPath;

        if (string.Equals(presetId, HorizontalBlackPresetId, StringComparison.OrdinalIgnoreCase)
            && File.Exists(HorizontalBlackLogoPath))
            return HorizontalBlackLogoPath;

        if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
            return Path.GetFullPath(customPath);

        return null;
    }

    public static string? DetectPresetId(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            var full = Path.GetFullPath(path);
            if (string.Equals(full, Path.GetFullPath(WhiteLogoPath), StringComparison.OrdinalIgnoreCase))
                return WhitePresetId;
            if (string.Equals(full, Path.GetFullPath(BlackLogoPath), StringComparison.OrdinalIgnoreCase))
                return BlackPresetId;
            if (string.Equals(full, Path.GetFullPath(HorizontalWhiteLogoPath), StringComparison.OrdinalIgnoreCase))
                return HorizontalWhitePresetId;
            if (string.Equals(full, Path.GetFullPath(HorizontalBlackLogoPath), StringComparison.OrdinalIgnoreCase))
                return HorizontalBlackPresetId;
        }
        catch
        {
            // yoksay
        }

        return null;
    }
}

public sealed record BrandLogoPreset(string Id, string Label, string FileName);
