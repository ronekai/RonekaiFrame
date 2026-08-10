namespace RonekaiImageFramer.Models;

public enum WatermarkCleanStyle
{
    Block = 0,
    Cloud = 1,
    /// <summary>Geniş yumuşak geçiş + güçlü ton uyumu.</summary>
    SoftHeal = 2,
    /// <summary>Yerel dokuyu koruyarak doldurur (az bulanık).</summary>
    TextureFill = 3,
    /// <summary>Çok net kenar, minimal tüy.</summary>
    SharpEdge = 4,
    /// <summary>Güçlü bulanık / duman yayılımı.</summary>
    DeepBlur = 5,
    /// <summary>Orta yumuşaklık + ekstra kenar pürüzsüzlüğü.</summary>
    Seamless = 6
}

/// <summary>Normalize edilmiş nokta (0..1 görsel uzayı).</summary>
public sealed record NormalizedPoint(double X, double Y);

/// <summary>
/// Filigram temizleme. Polygon (2+=çokgen/çizgi) veya fırça (merkez+yarıçap+şekil).
/// Koordinatlar kaynak görsel uzayındadır.
/// </summary>
public sealed record WatermarkCleanOp(
    WatermarkCleanStyle Style,
    IReadOnlyList<NormalizedPoint> Polygon,
    NormalizedPoint? BrushCenter = null,
    double BrushRadiusNorm = 0,
    TextureCloneBrushShape BrushShape = TextureCloneBrushShape.Circle,
    double RotationDegrees = 0)
{
    public bool IsBrush =>
        BrushCenter is not null && BrushRadiusNorm > 0.0005;
}
