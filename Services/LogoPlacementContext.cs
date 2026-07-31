namespace RonekaiImageFramer.Services;

/// <summary>
/// Şablon render sırasında marka şeridi / köşe filigranı için ayrılan alan (px).
/// AsyncLocal: eşzamanlı önizleme iş parçacıkları birbirinin rezervini bozmaz.
/// </summary>
public static class LogoPlacementContext
{
    private sealed class State
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

    private static readonly AsyncLocal<State?> Current = new();

    private static State Slot => Current.Value ??= new State();

    public static int Left => Slot.Left;
    public static int Right => Slot.Right;
    public static int Top => Slot.Top;
    public static int Bottom => Slot.Bottom;

    public static void Reset()
    {
        var s = Slot;
        s.Left = s.Right = s.Top = s.Bottom = 0;
    }

    public static void ReserveLeft(int pixels) => Slot.Left = Math.Max(Slot.Left, pixels);
    public static void ReserveRight(int pixels) => Slot.Right = Math.Max(Slot.Right, pixels);
    public static void ReserveTop(int pixels) => Slot.Top = Math.Max(Slot.Top, pixels);
    public static void ReserveBottom(int pixels) => Slot.Bottom = Math.Max(Slot.Bottom, pixels);

    /// <summary>Sağ alt köşe marka filigranı (DrawCornerWatermark ile uyumlu).</summary>
    public static void ReserveCornerBrand(int canvasWidth, int margin = 24)
    {
        float size = Math.Max(18, canvasWidth * 0.028f);
        ReserveRight(margin + (int)(size * 5.8f));
        ReserveBottom(margin + (int)(size * 1.5f));
    }
}
