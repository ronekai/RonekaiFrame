namespace RonekaiImageFramer.Ui;

/// <summary>WPF arayüz renkleri (ImageSharp ile aynı derlemede karışmaz).</summary>
public static class UiColorHelper
{
    public static string ToHex(byte r, byte g, byte b) =>
        $"#{r:X2}{g:X2}{b:X2}";

    public static System.Windows.Media.SolidColorBrush ToSolidBrush(string hex)
    {
        try
        {
            var color = ParseWpfColor(hex);
            return new System.Windows.Media.SolidColorBrush(color);
        }
        catch
        {
            return System.Windows.Media.Brushes.LightGray;
        }
    }

    public static System.Windows.Media.Color ParseWpfColor(string hex)
    {
        hex = hex.Trim();
        if (!hex.StartsWith('#')) hex = "#" + hex;
        return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)!;
    }

    public static (byte R, byte G, byte B) ParseRgb(string hex)
    {
        var c = ParseWpfColor(hex);
        return (c.R, c.G, c.B);
    }
}
