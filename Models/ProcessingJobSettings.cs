using System.Collections.Generic;

namespace RonekaiImageFramer.Models;

public sealed class ProcessingJobSettings
{
    public bool ResizeOnly { get; init; }
    public bool StretchToExport { get; init; }
    public bool ResponsiveProductFit { get; init; }
    public int JpegQuality { get; init; } = 92;
    public bool SaveAsPng { get; init; }
    public string FileNamePattern { get; init; } = "{base}";
    public TextOverlaySettings TextOverlay { get; init; } = new();
    public int SamplePreviewCount { get; init; }
    public bool ProcessOnlySelectedFiles { get; init; }
    public NormalizedCropRect? CropRect { get; init; }
    public bool CropOnlySelectedFiles { get; init; }
    public IReadOnlyList<string> CropSelectedFilePaths { get; init; } = [];

    /// <summary>Uygulanacak filigram temizleme işlemleri (sırayla).</summary>
    public IReadOnlyList<WatermarkCleanOp> WatermarkCleanOps { get; init; } = [];

    /// <summary>Klon damga (doku transferi) işlemleri — temizlikten sonra, logodan önce.</summary>
    public IReadOnlyList<TextureCloneOp> TextureCloneOps { get; init; } = [];

    public static ProcessingJobSettings Default => new();
}
