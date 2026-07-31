namespace RonekaiImageFramer.Models;

public enum WatermarkCleanStyle
{
    Block = 0,
    Cloud = 1
}

/// <summary>Normalize edilmiş nokta (0..1 görsel uzayı).</summary>
public sealed record NormalizedPoint(double X, double Y);

/// <summary>
/// Filigram temizleme işlemi. Polygon: pin sırası (2 = çizgi koridoru, 3+ = kapalı çokgen).
/// </summary>
public sealed record WatermarkCleanOp(
    WatermarkCleanStyle Style,
    IReadOnlyList<NormalizedPoint> Polygon);
