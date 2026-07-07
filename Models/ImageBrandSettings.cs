namespace RonekaiImageFramer.Models;

/// <summary>Şablon ve önizleme üzerine çizilen marka metni.</summary>
public sealed class ImageBrandSettings
{
    public string MainText { get; set; } = "RONEKAI";
    public string SuffixText { get; set; } = ".DEN";
    public string MainFontId { get; set; } = "segoe-ui";
    public string SuffixFontId { get; set; } = "segoe-ui";
    public bool ShowMainText { get; set; }
    public bool ShowSuffixText { get; set; }
    /// <summary>100 = şablona göre varsayılan ana metin boyutu.</summary>
    public int MainTextSizePercent { get; set; } = 100;
    /// <summary>100 = ana metne göre varsayılan ek metin oranı.</summary>
    public int SuffixTextSizePercent { get; set; } = 100;
    public bool ShowBrandLogo { get; set; }
    public string? BrandLogoPresetId { get; set; }
    public string? BrandLogoPath { get; set; }
    /// <summary>100 = şablona göre varsayılan marka logo boyutu.</summary>
    public int BrandLogoSizePercent { get; set; } = 100;
    /// <summary>0–1 şeffaflık.</summary>
    public float BrandLogoOpacity { get; set; } = 1f;
    public OverlayPlacement BrandLogoPlacement { get; set; } = OverlayPlacement.BottomRight;
    public int BrandLogoOffsetX { get; set; }
    public int BrandLogoOffsetY { get; set; }
    public bool BrandLogoTintEnabled { get; set; }
    public ThemeColorAppearance BrandLogoTint { get; set; } = ThemeColorAppearance.FromHex("#1B2A4A", "#C9A227");

    public static ImageBrandSettings CreateDefault() => new();

    public ImageBrandSettings Clone() => new()
    {
        MainText = MainText,
        SuffixText = SuffixText,
        MainFontId = MainFontId,
        SuffixFontId = SuffixFontId,
        ShowMainText = ShowMainText,
        ShowSuffixText = ShowSuffixText,
        MainTextSizePercent = MainTextSizePercent,
        SuffixTextSizePercent = SuffixTextSizePercent,
        ShowBrandLogo = ShowBrandLogo,
        BrandLogoPresetId = BrandLogoPresetId,
        BrandLogoPath = BrandLogoPath,
        BrandLogoSizePercent = BrandLogoSizePercent,
        BrandLogoOpacity = BrandLogoOpacity,
        BrandLogoPlacement = BrandLogoPlacement,
        BrandLogoOffsetX = BrandLogoOffsetX,
        BrandLogoOffsetY = BrandLogoOffsetY,
        BrandLogoTintEnabled = BrandLogoTintEnabled,
        BrandLogoTint = BrandLogoTint.Clone()
    };
}
