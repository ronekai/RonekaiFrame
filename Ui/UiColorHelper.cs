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

    public static string ToRgbString(byte r, byte g, byte b) => $"{r}, {g}, {b}";

    public static string NormalizeHex(string input)
    {
        var hex = input.Trim();
        if (hex.StartsWith('#'))
            hex = hex[1..];
        hex = hex.TrimStart('#');
        if (hex.Length is 3)
            hex = string.Concat(hex.Select(c => $"{c}{c}"));
        if (hex.Length != 6)
            throw new FormatException("Hex 6 karakter olmalı (#RRGGBB).");
        return $"#{hex.ToUpperInvariant()}";
    }

    public static bool TryParseHex(string? input, out string normalizedHex)
    {
        normalizedHex = "#F5F6F8";
        if (string.IsNullOrWhiteSpace(input))
            return false;
        try
        {
            normalizedHex = NormalizeHex(input);
            _ = ParseWpfColor(normalizedHex);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>245,246,248 veya 245 246 248</summary>
    public static bool TryParseRgbString(string? input, out string normalizedHex)
    {
        normalizedHex = "#F5F6F8";
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var parts = input.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
            return false;

        if (!byte.TryParse(parts[0].Trim(), out byte r) ||
            !byte.TryParse(parts[1].Trim(), out byte g) ||
            !byte.TryParse(parts[2].Trim(), out byte b))
            return false;

        normalizedHex = ToHex(r, g, b);
        return true;
    }

    public static bool TryParseColorInput(string? input, out string normalizedHex) =>
        TryParseHex(input, out normalizedHex) || TryParseRgbString(input, out normalizedHex);
}
