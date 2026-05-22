namespace RonekaiImageFramer.Models;

/// <summary>Şablon ve önizleme üzerine çizilen marka metni.</summary>
public sealed class ImageBrandSettings
{
    public string MainText { get; set; } = "RONEKAI";
    public string SuffixText { get; set; } = ".DEN";
    public string MainFontId { get; set; } = "segoe-ui";
    public string SuffixFontId { get; set; } = "segoe-ui";
    public bool ShowMainText { get; set; } = true;
    public bool ShowSuffixText { get; set; } = true;
    /// <summary>100 = şablona göre varsayılan ana metin boyutu.</summary>
    public int MainTextSizePercent { get; set; } = 100;
    /// <summary>100 = ana metne göre varsayılan ek metin oranı.</summary>
    public int SuffixTextSizePercent { get; set; } = 100;

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
        SuffixTextSizePercent = SuffixTextSizePercent
    };
}
