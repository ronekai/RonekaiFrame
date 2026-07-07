namespace RonekaiImageFramer.Models;

public sealed class ProcessingPreset
{
    public string Name { get; set; } = "Varsayılan";
    public string TemplateId { get; set; } = "white-studio";
    public string ColorPackId { get; set; } = "klasik";
    public string ExportProfileId { get; set; } = "template-default";
    public string LogoModeId { get; set; } = "None";
    public bool UseDefaultLogo { get; set; } = true;
    public string? CustomLogoPath { get; set; }
    public float LogoOpacity { get; set; } = 0.35f;
    public string LogoPlacementId { get; set; } = "Center";
    public int LogoScalePercent { get; set; } = 62;
    public string ImageBrandMain { get; set; } = "RONEKAI";
    public string ImageBrandSuffix { get; set; } = ".DEN";
    public string BrandMainFontId { get; set; } = "segoe-ui";
    public string BrandSuffixFontId { get; set; } = "segoe-ui";
    public bool ImageBrandShowMain { get; set; }
    public bool ImageBrandShowSuffix { get; set; }
    public int ImageBrandMainSizePercent { get; set; } = 100;
    public int ImageBrandSuffixSizePercent { get; set; } = 100;
    public bool ImageBrandShowLogo { get; set; }
    public string? ImageBrandLogoPath { get; set; }
    public int ImageBrandLogoSizePercent { get; set; } = 100;
    public float ImageBrandLogoOpacity { get; set; } = 1f;
    public string ImageBrandLogoPlacementId { get; set; } = "BottomRight";
    public int BrandLogoOffsetX { get; set; }
    public int BrandLogoOffsetY { get; set; }
    public bool BrandLogoTintEnabled { get; set; }
    public ThemeColorAppearance? BrandLogoTint { get; set; }
    public string CustomBackgroundHex { get; set; } = "#F5F6F8";
    public string CustomRonekaiHex { get; set; } = "#1B2A4A";
    public string CustomDenHex { get; set; } = "#C9A227";
    public ThemeColorSet? ThemeColors { get; set; }
    public bool ResizeOnly { get; set; }
    public bool ResponsiveProductFit { get; set; }
    public int JpegQuality { get; set; } = 92;
    public bool SaveAsPng { get; set; }
    public string FileNamePattern { get; set; } = "{base}";
    public bool TextOverlayEnabled { get; set; }
    public string TextOverlayText { get; set; } = "";
    public string TextOverlayPosition { get; set; } = "BottomCenter";
    public int SamplePreviewCount { get; set; }
    public bool ProcessSelectedOnly { get; set; }
    public string? SourceFolderPath { get; set; }
}
