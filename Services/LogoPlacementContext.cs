namespace RonekaiImageFramer.Services;

/// <summary>
/// Şablon render sırasında marka şeridi / köşe filigranı için ayrılan alan (px).
/// Logo katmanı rozet ve çerçeve modlarında bu bölgelere taşınmaz.
/// </summary>
public static class LogoPlacementContext
{
    public static int Left { get; private set; }
    public static int Right { get; private set; }
    public static int Top { get; private set; }
    public static int Bottom { get; private set; }

    public static void Reset()
    {
        Left = Right = Top = Bottom = 0;
    }

    public static void ReserveLeft(int pixels) => Left = Math.Max(Left, pixels);
    public static void ReserveRight(int pixels) => Right = Math.Max(Right, pixels);
    public static void ReserveTop(int pixels) => Top = Math.Max(Top, pixels);
    public static void ReserveBottom(int pixels) => Bottom = Math.Max(Bottom, pixels);

    /// <summary>Sağ alt köşe marka filigranı (DrawCornerWatermark ile uyumlu).</summary>
    public static void ReserveCornerBrand(int canvasWidth, int margin = 24)
    {
        float size = Math.Max(18, canvasWidth * 0.028f);
        ReserveRight(margin + (int)(size * 5.8f));
        ReserveBottom(margin + (int)(size * 1.5f));
    }
}
