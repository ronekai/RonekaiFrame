using ImgSize = SixLabors.ImageSharp.Size;

namespace RonekaiImageFramer.Services;

/// <summary>
/// Kaynak görseli seçilen orana sığdıran tuval boyutu.
/// Büyük görsellerde uzun kenarı 100 px’e yuvarlar (ör. 2384×2200 → 1:1 = 2400×2400).
/// </summary>
public static class SmartCanvasSize
{
    public static ImgSize Resolve(
        int sourceWidth,
        int sourceHeight,
        int ratioW,
        int ratioH,
        int minWidth,
        int minHeight)
    {
        sourceWidth = Math.Max(1, sourceWidth);
        sourceHeight = Math.Max(1, sourceHeight);
        minWidth = Math.Max(1, minWidth);
        minHeight = Math.Max(1, minHeight);

        int g = Gcd(ratioW, ratioH);
        ratioW = Math.Max(1, ratioW / g);
        ratioH = Math.Max(1, ratioH / g);

        // Kaynağı bozmadan içeren en küçük oranlı tuval
        double kNeeded = Math.Max(sourceWidth / (double)ratioW, sourceHeight / (double)ratioH);
        double rawW = kNeeded * ratioW;
        double rawH = kNeeded * ratioH;

        int outW;
        int outH;
        if (rawW >= rawH)
        {
            outW = SnapUpToRatio(RoundUpNice100(rawW), ratioW);
            while (outW + 1e-6 < rawW)
                outW += ratioW;
            outH = outW / ratioW * ratioH;
        }
        else
        {
            outH = SnapUpToRatio(RoundUpNice100(rawH), ratioH);
            while (outH + 1e-6 < rawH)
                outH += ratioH;
            outW = outH / ratioH * ratioW;
        }

        if (outW < minWidth || outH < minHeight)
        {
            double scale = Math.Max(minWidth / (double)outW, minHeight / (double)outH);
            int units = (int)Math.Ceiling(Math.Max(
                outW * scale / ratioW,
                outH * scale / ratioH));
            units = Math.Max(1, units);
            outW = units * ratioW;
            outH = units * ratioH;
        }

        return new ImgSize(Math.Max(1, outW), Math.Max(1, outH));
    }

    /// <summary>En yakın 100’e; ham değerin altına düşmez (2384 → 2400).</summary>
    public static int RoundUpNice100(double raw)
    {
        if (raw <= 100)
            return 100;
        int nearest = (int)Math.Round(raw / 100.0) * 100;
        if (nearest < raw)
            nearest += 100;
        return Math.Max(100, nearest);
    }

    private static int SnapUpToRatio(int value, int ratioPart)
    {
        if (ratioPart <= 1)
            return value;
        int rem = value % ratioPart;
        return rem == 0 ? value : value + (ratioPart - rem);
    }

    private static int Gcd(int a, int b)
    {
        a = Math.Abs(a);
        b = Math.Abs(b);
        while (b != 0)
        {
            int t = a % b;
            a = b;
            b = t;
        }
        return Math.Max(1, a);
    }
}
