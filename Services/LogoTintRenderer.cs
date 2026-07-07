using RonekaiImageFramer.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace RonekaiImageFramer.Services;

public static class LogoTintRenderer
{
    public static Image<Rgba32> Apply(Image<Rgba32> source, ThemeColorAppearance appearance)
    {
        var result = source.CloneAs<Rgba32>();
        int w = result.Width;
        int h = result.Height;
        if (w == 0 || h == 0)
            return result;

        result.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    ref Rgba32 p = ref row[x];
                    if (p.A == 0)
                        continue;

                    var tint = Sample(appearance, x, y, w, h);
                    byte a = (byte)Math.Clamp(p.A * appearance.Opacity, 0, 255);
                    row[x] = new Rgba32(tint.R, tint.G, tint.B, a);
                }
            }
        });

        return result;
    }

    private static Rgba32 Sample(ThemeColorAppearance appearance, int x, int y, int w, int h)
    {
        if (appearance.FillMode == ColorFillMode.Solid)
            return HexToRgba(appearance.PrimaryHex);

        float t = appearance.GradientDirection switch
        {
            GradientDirection.Horizontal => x / (float)Math.Max(1, w - 1),
            GradientDirection.DiagonalDown => (x + y) / (float)Math.Max(1, w + h - 2),
            GradientDirection.DiagonalUp => (x + (h - 1 - y)) / (float)Math.Max(1, w + h - 2),
            _ => y / (float)Math.Max(1, h - 1)
        };

        t = Math.Clamp(t, 0f, 1f);
        var start = HexToRgba(appearance.PrimaryHex);
        var end = HexToRgba(appearance.GradientEndHex);
        return new Rgba32(
            (byte)(start.R + (end.R - start.R) * t),
            (byte)(start.G + (end.G - start.G) * t),
            (byte)(start.B + (end.B - start.B) * t),
            255);
    }

    private static Rgba32 HexToRgba(string hex)
    {
        hex = hex.Trim();
        if (!hex.StartsWith('#'))
            hex = "#" + hex;
        if (hex.Length is not (7 or 9))
            return new Rgba32(27, 42, 74, 255);

        byte r = Convert.ToByte(hex.Substring(1, 2), 16);
        byte g = Convert.ToByte(hex.Substring(3, 2), 16);
        byte b = Convert.ToByte(hex.Substring(5, 2), 16);
        return new Rgba32(r, g, b, 255);
    }
}
