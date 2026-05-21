namespace RonekaiImageFramer.Models;

/// <summary>Şablon ve önizleme üzerine çizilen marka metni.</summary>
public sealed class ImageBrandSettings
{
    public string MainText { get; set; } = "RONEKAI";
    public string SuffixText { get; set; } = ".DEN";

    public static ImageBrandSettings CreateDefault() => new();

    public ImageBrandSettings Clone() => new()
    {
        MainText = MainText,
        SuffixText = SuffixText
    };
}
