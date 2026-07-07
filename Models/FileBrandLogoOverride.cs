namespace RonekaiImageFramer.Models;

/// <summary>Tek bir kaynak görsel için marka logosu ayarları.</summary>
public sealed class FileBrandLogoOverride
{
    public bool Enabled { get; set; } = true;
    /// <summary>white | black | null = özel dosya yolu</summary>
    public string? LogoPresetId { get; set; }
    public string? LogoPath { get; set; }
    public int SizePercent { get; set; } = 100;
    public float Opacity { get; set; } = 1f;
    public string PlacementId { get; set; } = "BottomRight";
    public int OffsetX { get; set; }
    public int OffsetY { get; set; }
    public bool BrandLogoTintEnabled { get; set; }
    public ThemeColorAppearance BrandLogoTint { get; set; } = ThemeColorAppearance.FromHex("#1B2A4A", "#C9A227");

    public FileBrandLogoOverride Clone() => new()
    {
        Enabled = Enabled,
        LogoPresetId = LogoPresetId,
        LogoPath = LogoPath,
        SizePercent = SizePercent,
        Opacity = Opacity,
        PlacementId = PlacementId,
        OffsetX = OffsetX,
        OffsetY = OffsetY,
        BrandLogoTintEnabled = BrandLogoTintEnabled,
        BrandLogoTint = BrandLogoTint.Clone()
    };
}
