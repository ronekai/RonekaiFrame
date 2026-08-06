namespace RonekaiImageFramer.Services;

/// <summary>
/// Son şablon çiziminde ürünün tuval üzerindeki yerleşimi.
/// Filigram/klon noktalarını kaynak görsel uzayına taşımak için kullanılır.
/// </summary>
public static class ProductPlacementContext
{
    private static readonly object Gate = new();

    public static bool HasPlacement { get; private set; }
    public static int CanvasWidth { get; private set; }
    public static int CanvasHeight { get; private set; }
    public static int SourceWidth { get; private set; }
    public static int SourceHeight { get; private set; }
    public static int DestX { get; private set; }
    public static int DestY { get; private set; }
    public static int DestWidth { get; private set; }
    public static int DestHeight { get; private set; }

    public static void Set(
        int sourceWidth, int sourceHeight,
        int canvasWidth, int canvasHeight,
        int destX, int destY, int destWidth, int destHeight)
    {
        lock (Gate)
        {
            SourceWidth = Math.Max(1, sourceWidth);
            SourceHeight = Math.Max(1, sourceHeight);
            CanvasWidth = Math.Max(1, canvasWidth);
            CanvasHeight = Math.Max(1, canvasHeight);
            DestX = destX;
            DestY = destY;
            DestWidth = Math.Max(1, destWidth);
            DestHeight = Math.Max(1, destHeight);
            HasPlacement = true;
        }
    }

    public static void SetIdentity(int width, int height)
    {
        Set(width, height, width, height, 0, 0, width, height);
    }

    public static void Clear()
    {
        lock (Gate)
        {
            HasPlacement = false;
        }
    }

    public static bool CanvasNormToSourceNorm(double cx, double cy, out double sx, out double sy)
    {
        lock (Gate)
        {
            if (!HasPlacement)
            {
                sx = Math.Clamp(cx, 0, 1);
                sy = Math.Clamp(cy, 0, 1);
                return false;
            }

            double px = Math.Clamp(cx, 0, 1) * (CanvasWidth - 1);
            double py = Math.Clamp(cy, 0, 1) * (CanvasHeight - 1);
            double u = (px - DestX) / DestWidth;
            double v = (py - DestY) / DestHeight;
            sx = Math.Clamp(u, 0, 1);
            sy = Math.Clamp(v, 0, 1);
            return u >= -0.02 && u <= 1.02 && v >= -0.02 && v <= 1.02;
        }
    }

    public static bool SourceNormToCanvasNorm(double sx, double sy, out double cx, out double cy)
    {
        lock (Gate)
        {
            if (!HasPlacement)
            {
                cx = Math.Clamp(sx, 0, 1);
                cy = Math.Clamp(sy, 0, 1);
                return false;
            }

            double px = DestX + Math.Clamp(sx, 0, 1) * DestWidth;
            double py = DestY + Math.Clamp(sy, 0, 1) * DestHeight;
            cx = CanvasWidth <= 1 ? 0 : Math.Clamp(px / (CanvasWidth - 1), 0, 1);
            cy = CanvasHeight <= 1 ? 0 : Math.Clamp(py / (CanvasHeight - 1), 0, 1);
            return true;
        }
    }

    /// <summary>Kaynak kısa kenar yarıçapını tuval pikseline çevirir.</summary>
    public static double SourceRadiusNormToCanvasPx(double radiusNorm)
    {
        lock (Gate)
        {
            if (!HasPlacement)
                return Math.Max(2, radiusNorm * 100);

            double sourceShort = Math.Min(SourceWidth, SourceHeight);
            double scale = Math.Min(DestWidth / (double)SourceWidth, DestHeight / (double)SourceHeight);
            return Math.Max(2, radiusNorm * sourceShort * scale);
        }
    }

    /// <summary>
    /// Tuval yarıçapını önizleme görselinin ekran pikseline çevirir (zoom/letterbox dahil).
    /// </summary>
    public static double SourceRadiusNormToDisplayPx(double radiusNorm, double renderedCanvasWidthPx)
    {
        double canvasPx = SourceRadiusNormToCanvasPx(radiusNorm);
        lock (Gate)
        {
            if (!HasPlacement || CanvasWidth < 2)
                return Math.Max(2, radiusNorm * Math.Max(2, renderedCanvasWidthPx));
            return Math.Max(2, canvasPx * (renderedCanvasWidthPx / CanvasWidth));
        }
    }
}
