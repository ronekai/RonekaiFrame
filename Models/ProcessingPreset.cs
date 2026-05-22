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
    public string ImageBrandMain { get; set; } = "RONEKAI";
    public string ImageBrandSuffix { get; set; } = ".DEN";
    public string BrandMainFontId { get; set; } = "segoe-ui";
    public string BrandSuffixFontId { get; set; } = "segoe-ui";
    public bool ImageBrandShowMain { get; set; } = true;
    public bool ImageBrandShowSuffix { get; set; } = true;
    public int ImageBrandMainSizePercent { get; set; } = 100;
    public int ImageBrandSuffixSizePercent { get; set; } = 100;
    public string CustomBackgroundHex { get; set; } = "#F5F6F8";
    public string CustomRonekaiHex { get; set; } = "#1B2A4A";
    public string CustomDenHex { get; set; } = "#C9A227";
    public ThemeColorSet? ThemeColors { get; set; }
    public bool ResizeOnly { get; set; }
    public bool ResponsiveProductFit { get; set; }
    public int JpegQuality { get; set; } = 92;
    public bool SaveAsPng { get; set; }
    public string FileNamePattern { get; set; } = "{base}_{stamp}_{template}_{export}";
    public bool TextOverlayEnabled { get; set; }
    public string TextOverlayText { get; set; } = "";
    public string TextOverlayPosition { get; set; } = "BottomCenter";
    public int SamplePreviewCount { get; set; }
    public bool ProcessSelectedOnly { get; set; }
    public string? SourceFolderPath { get; set; }
}
