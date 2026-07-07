namespace RonekaiImageFramer.Models;

public sealed class ProcessingJobSettings
{
    public bool ResizeOnly { get; init; }

    /// <summary>Yay şablonu: seçili çıktı boyutuna tam yay.</summary>
    public bool StretchToExport { get; init; }

    /// <summary>Ürünü şablon alanına cover ile doldurur (boşluk azalır, kenarlar kırpılabilir).</summary>
    public bool ResponsiveProductFit { get; init; }
    public int JpegQuality { get; init; } = 92;
    public bool SaveAsPng { get; init; }
    public string FileNamePattern { get; init; } = "{base}";
    public TextOverlaySettings TextOverlay { get; init; } = new();
    public int SamplePreviewCount { get; init; }
    public bool ProcessOnlySelectedFiles { get; init; }

    public static ProcessingJobSettings Default => new();
}
