using RonekaiImageFramer.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace RonekaiImageFramer.Services;

/// <summary>
/// Şablon letterbox (sol/sağ veya üst/alt) boşluklarını fotoğraf kenarındaki
/// zemin tonuyla uzatarak doldurur — saf beyaz palet ile gri stüdyo zemini farkını kapatır.
/// </summary>
public static class EdgePadFillService
{
    /// <summary>Birleşim çizgisini kapatmak için ürüne doğru taşırma (px).</summary>
    private const int SeamOverlap = 2;

    /// <param name="sampleRect">
    /// Tuval normalize örnek şerit. null = otomatik kenar.
    /// Dikey şerit (boy &gt; en) → sol/sağ; yatay şerit → üst/alt.
    /// </param>
    public static void Apply(Image<Rgba32> canvas, NormalizedCropRect? sampleRect = null)
    {
        if (canvas.Width < 4 || canvas.Height < 4)
            return;
        if (!ProductPlacementContext.HasPlacement)
            return;

        int dx = ProductPlacementContext.DestX;
        int dy = ProductPlacementContext.DestY;
        int dw = ProductPlacementContext.DestWidth;
        int dh = ProductPlacementContext.DestHeight;
        int cw = canvas.Width;
        int ch = canvas.Height;

        dx = Math.Clamp(dx, 0, cw - 1);
        dy = Math.Clamp(dy, 0, ch - 1);
        dw = Math.Clamp(dw, 1, cw - dx);
        dh = Math.Clamp(dh, 1, ch - dy);

        bool left = dx > 0;
        bool right = dx + dw < cw;
        bool top = dy > 0;
        bool bottom = dy + dh < ch;
        if (!left && !right && !top && !bottom)
            return;

        if (sampleRect is { } strip)
        {
            ApplyManualStrip(canvas, dx, dy, dw, dh, strip);
            return;
        }

        int sampleW = Math.Clamp(Math.Max(3, dw / 40), 3, 40);
        int sampleH = Math.Clamp(Math.Max(3, dh / 40), 3, 40);

        if (left)
            FillHorizontalPad(canvas, writeLeft: 0, writeRight: dx + SeamOverlap, sampleX: dx, sampleDepth: sampleW, dy, dh);
        if (right)
            FillHorizontalPad(canvas, writeLeft: dx + dw - SeamOverlap, writeRight: cw, sampleX: dx + dw - sampleW, sampleDepth: sampleW, dy, dh);
        if (top)
            FillVerticalPad(canvas, writeTop: 0, writeBottom: dy + SeamOverlap, sampleY: dy, sampleDepth: sampleH, dx, dw);
        if (bottom)
            FillVerticalPad(canvas, writeTop: dy + dh - SeamOverlap, writeBottom: ch, sampleY: dy + dh - sampleH, sampleDepth: sampleH, dx, dw);
    }

    private static void ApplyManualStrip(
        Image<Rgba32> canvas,
        int dx, int dy, int dw, int dh,
        NormalizedCropRect strip)
    {
        int cw = canvas.Width;
        int ch = canvas.Height;
        int sx0 = Math.Clamp((int)Math.Floor(strip.Left * cw), 0, cw - 1);
        int sy0 = Math.Clamp((int)Math.Floor(strip.Top * ch), 0, ch - 1);
        int sx1 = Math.Clamp((int)Math.Ceiling((strip.Left + strip.Width) * cw), sx0 + 1, cw);
        int sy1 = Math.Clamp((int)Math.Ceiling((strip.Top + strip.Height) * ch), sy0 + 1, ch);
        int sw = sx1 - sx0;
        int sh = sy1 - sy0;

        bool verticalStrip = sh >= sw * 1.25;
        bool horizontalStrip = sw >= sh * 1.25;

        if (verticalStrip || (!horizontalStrip && (dx > 0 || dx + dw < cw)))
        {
            int sampleDepth = Math.Clamp(sw, 1, 64);
            int sampleX = Math.Clamp(sx0, dx, Math.Max(dx, dx + dw - sampleDepth));
            if (dx > 0)
                FillHorizontalPad(canvas, 0, dx + SeamOverlap, sampleX, sampleDepth, 0, ch, fullHeight: true);
            if (dx + dw < cw)
            {
                int rx = Math.Clamp(sx1 - sampleDepth, dx, Math.Max(dx, dx + dw - sampleDepth));
                FillHorizontalPad(canvas, dx + dw - SeamOverlap, cw, rx, sampleDepth, 0, ch, fullHeight: true);
            }
            return;
        }

        int sampleDepthH = Math.Clamp(sh, 1, 64);
        int sampleY = Math.Clamp(sy0, dy, Math.Max(dy, dy + dh - sampleDepthH));
        if (dy > 0)
            FillVerticalPad(canvas, 0, dy + SeamOverlap, sampleY, sampleDepthH, 0, cw, fullWidth: true);
        if (dy + dh < ch)
        {
            int by = Math.Clamp(sy1 - sampleDepthH, dy, Math.Max(dy, dy + dh - sampleDepthH));
            FillVerticalPad(canvas, dy + dh - SeamOverlap, ch, by, sampleDepthH, 0, cw, fullWidth: true);
        }
    }

    /// <summary>Sol/sağ pad: örnek dikey şeridi yatayda yayar; birleşime 2 px taşır (ince beyaz çizgi olmasın).</summary>
    private static void FillHorizontalPad(
        Image<Rgba32> canvas,
        int writeLeft, int writeRight,
        int sampleX, int sampleDepth,
        int productY, int productH,
        bool fullHeight = false)
    {
        writeLeft = Math.Clamp(writeLeft, 0, canvas.Width);
        writeRight = Math.Clamp(writeRight, writeLeft, canvas.Width);
        if (writeRight <= writeLeft)
            return;

        int cw = canvas.Width;
        int ch = canvas.Height;
        sampleX = Math.Clamp(sampleX, 0, cw - 1);
        sampleDepth = Math.Clamp(sampleDepth, 1, Math.Max(1, cw - sampleX));
        int y0 = fullHeight ? 0 : Math.Clamp(productY, 0, ch - 1);
        int y1 = fullHeight ? ch : Math.Clamp(productY + productH, y0 + 1, ch);
        int span = y1 - y0;
        if (span < 1)
            return;

        // Önce örnekle (yazmadan), sonra yaz — overlap ürün piksellerini bozmasın diye
        var samples = new Rgba32[span];
        canvas.ProcessPixelRows(accessor =>
        {
            for (int i = 0; i < span; i++)
            {
                var row = accessor.GetRowSpan(y0 + i);
                samples[i] = AverageSpan(row, sampleX, sampleDepth);
            }
        });

        canvas.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < ch; y++)
            {
                int si = Math.Clamp(y - y0, 0, span - 1);
                var sample = samples[si];
                var row = accessor.GetRowSpan(y);
                for (int x = writeLeft; x < writeRight; x++)
                    row[x] = sample;
            }
        });
    }

    /// <summary>Üst/alt pad: örnek yatay şeridi dikeyde yayar; birleşime 2 px taşır.</summary>
    private static void FillVerticalPad(
        Image<Rgba32> canvas,
        int writeTop, int writeBottom,
        int sampleY, int sampleDepth,
        int productX, int productW,
        bool fullWidth = false)
    {
        writeTop = Math.Clamp(writeTop, 0, canvas.Height);
        writeBottom = Math.Clamp(writeBottom, writeTop, canvas.Height);
        if (writeBottom <= writeTop)
            return;

        int cw = canvas.Width;
        int ch = canvas.Height;
        sampleY = Math.Clamp(sampleY, 0, ch - 1);
        sampleDepth = Math.Clamp(sampleDepth, 1, Math.Max(1, ch - sampleY));
        int x0 = fullWidth ? 0 : Math.Clamp(productX, 0, cw - 1);
        int x1 = fullWidth ? cw : Math.Clamp(productX + productW, x0 + 1, cw);
        int span = x1 - x0;
        if (span < 1)
            return;

        var samples = new Rgba32[span];
        canvas.ProcessPixelRows(accessor =>
        {
            var accR = new int[span];
            var accG = new int[span];
            var accB = new int[span];
            var accA = new int[span];
            for (int dy = 0; dy < sampleDepth; dy++)
            {
                int y = Math.Clamp(sampleY + dy, 0, ch - 1);
                var row = accessor.GetRowSpan(y);
                for (int i = 0; i < span; i++)
                {
                    var p = row[x0 + i];
                    accR[i] += p.R; accG[i] += p.G; accB[i] += p.B; accA[i] += p.A;
                }
            }

            for (int i = 0; i < span; i++)
            {
                samples[i] = new Rgba32(
                    (byte)(accR[i] / sampleDepth),
                    (byte)(accG[i] / sampleDepth),
                    (byte)(accB[i] / sampleDepth),
                    (byte)(accA[i] / sampleDepth));
            }
        });

        canvas.ProcessPixelRows(accessor =>
        {
            for (int y = writeTop; y < writeBottom; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < cw; x++)
                {
                    int si = Math.Clamp(x - x0, 0, span - 1);
                    row[x] = samples[si];
                }
            }
        });
    }

    private static Rgba32 AverageSpan(Span<Rgba32> row, int start, int depth)
    {
        start = Math.Clamp(start, 0, row.Length - 1);
        depth = Math.Clamp(depth, 1, row.Length - start);
        int r = 0, g = 0, b = 0, a = 0;
        for (int i = 0; i < depth; i++)
        {
            var p = row[start + i];
            r += p.R; g += p.G; b += p.B; a += p.A;
        }

        return new Rgba32((byte)(r / depth), (byte)(g / depth), (byte)(b / depth), (byte)(a / depth));
    }
}
