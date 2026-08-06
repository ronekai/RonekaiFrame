using RonekaiImageFramer.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace RonekaiImageFramer.Services;

/// <summary>
/// Klon damga: kaynak şeklini hedefe yumuşak kenar + hafif ton uyumu ile aktarır.
/// </summary>
public static class TextureCloneService
{
    public static void ApplyAll(Image<Rgba32> image, IReadOnlyList<TextureCloneOp> ops)
    {
        if (ops is null || ops.Count == 0)
            return;
        foreach (var op in ops)
            Apply(image, op);
    }

    public static void Apply(Image<Rgba32> image, TextureCloneOp op)
    {
        if (image.Width < 4 || image.Height < 4)
            return;

        if (op.FillRect is { } fill)
        {
            ApplyFillRect(image, op, fill);
            return;
        }

        int w = image.Width;
        int h = image.Height;
        float shortEdge = Math.Min(w, h);

        float radiusNorm = (float)Math.Clamp(op.RadiusNorm, 0.002, 0.25);
        float radius = Math.Max(1.5f, radiusNorm * shortEdge);
        // Yumuşak kenar seçimin İÇİNDE kalsın — dışarı taşmasın
        float feather = Math.Max(0.5f, radius * 0.18f);
        float coreR = Math.Max(0.5f, radius - feather);
        float outerR = radius;
        var shape = op.Shape;

        // Elips / kare için ROI (seçim sınırında)
        float roiR = shape switch
        {
            TextureCloneBrushShape.Ellipse => Math.Max(outerR, outerR * 0.62f) + 1f,
            TextureCloneBrushShape.Square or TextureCloneBrushShape.SoftSquare or TextureCloneBrushShape.Normal
                => outerR + 1f,
            _ => outerR + 1f
        };

        float dxC = (float)(Math.Clamp(op.DestCenter.X, 0, 1) * (w - 1));
        float dyC = (float)(Math.Clamp(op.DestCenter.Y, 0, 1) * (h - 1));
        float sxC = (float)(Math.Clamp(op.SourceCenter.X, 0, 1) * (w - 1));
        float syC = (float)(Math.Clamp(op.SourceCenter.Y, 0, 1) * (h - 1));
        float offX = sxC - dxC;
        float offY = syC - dyC;

        int x0 = Math.Clamp((int)Math.Floor(Math.Min(dxC, sxC) - roiR) - 2, 0, w - 1);
        int y0 = Math.Clamp((int)Math.Floor(Math.Min(dyC, syC) - roiR) - 2, 0, h - 1);
        int x1 = Math.Clamp((int)Math.Ceiling(Math.Max(dxC, sxC) + roiR) + 2, x0 + 1, w);
        int y1 = Math.Clamp((int)Math.Ceiling(Math.Max(dyC, syC) + roiR) + 2, y0 + 1, h);
        int rw = x1 - x0;
        int rh = y1 - y0;

        var bufR = new float[rw * rh];
        var bufG = new float[rw * rh];
        var bufB = new float[rw * rh];
        var bufA = new float[rw * rh];

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < rh; y++)
            {
                var row = accessor.GetRowSpan(y0 + y);
                for (int x = 0; x < rw; x++)
                {
                    int i = y * rw + x;
                    var p = row[x0 + x];
                    bufR[i] = p.R;
                    bufG[i] = p.G;
                    bufB[i] = p.B;
                    bufA[i] = p.A;
                }
            }
        });

        float ringInner = coreR * 0.92f;
        float srcSumR = 0, srcSumG = 0, srcSumB = 0;
        float dstSumR = 0, dstSumG = 0, dstSumB = 0;
        int srcCount = 0, dstCount = 0;

        for (int y = 0; y < rh; y++)
        {
            for (int x = 0; x < rw; x++)
            {
                float gx = x0 + x + 0.5f;
                float gy = y0 + y + 0.5f;
                int i = y * rw + x;

                float dd = ShapeDistance(gx, gy, dxC, dyC, radius, shape);
                if (dd >= ringInner && dd <= outerR)
                {
                    dstSumR += bufR[i]; dstSumG += bufG[i]; dstSumB += bufB[i];
                    dstCount++;
                }

                float sd = ShapeDistance(gx, gy, sxC, syC, radius, shape);
                if (sd >= ringInner && sd <= outerR)
                {
                    srcSumR += bufR[i]; srcSumG += bufG[i]; srcSumB += bufB[i];
                    srcCount++;
                }
            }
        }

        float shiftR = 0, shiftG = 0, shiftB = 0;
        if (srcCount > 8 && dstCount > 8)
        {
            const float strength = 0.55f;
            shiftR = ((dstSumR / dstCount) - (srcSumR / srcCount)) * strength;
            shiftG = ((dstSumG / dstCount) - (srcSumG / srcCount)) * strength;
            shiftB = ((dstSumB / dstCount) - (srcSumB / srcCount)) * strength;
        }

        var outR = (float[])bufR.Clone();
        var outG = (float[])bufG.Clone();
        var outB = (float[])bufB.Clone();
        var outA = (float[])bufA.Clone();

        for (int y = 0; y < rh; y++)
        {
            for (int x = 0; x < rw; x++)
            {
                float gx = x0 + x + 0.5f;
                float gy = y0 + y + 0.5f;
                float dist = ShapeDistance(gx, gy, dxC, dyC, radius, shape);
                if (dist > outerR)
                    continue;

                float blend = dist <= coreR
                    ? 1f
                    : SoftStep(Math.Clamp(1f - (dist - coreR) / Math.Max(0.5f, feather), 0f, 1f));
                if (blend < 0.01f)
                    continue;

                float sx = gx + offX;
                float sy = gy + offY;
                if (!SampleBilinear(bufR, bufG, bufB, bufA, rw, rh, x0, y0, sx, sy,
                        out float sr, out float sg, out float sb, out float sa))
                    continue;

                sr = Math.Clamp(sr + shiftR, 0, 255);
                sg = Math.Clamp(sg + shiftG, 0, 255);
                sb = Math.Clamp(sb + shiftB, 0, 255);

                int i = y * rw + x;
                outR[i] = bufR[i] * (1f - blend) + sr * blend;
                outG[i] = bufG[i] * (1f - blend) + sg * blend;
                outB[i] = bufB[i] * (1f - blend) + sb * blend;
                outA[i] = bufA[i] * (1f - blend) + sa * blend;
            }
        }

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < rh; y++)
            {
                var row = accessor.GetRowSpan(y0 + y);
                for (int x = 0; x < rw; x++)
                {
                    int i = y * rw + x;
                    row[x0 + x] = new Rgba32(
                        (byte)Math.Clamp(outR[i] + 0.5f, 0, 255),
                        (byte)Math.Clamp(outG[i] + 0.5f, 0, 255),
                        (byte)Math.Clamp(outB[i] + 0.5f, 0, 255),
                        (byte)Math.Clamp(outA[i] + 0.5f, 0, 255));
                }
            }
        });
    }

    /// <summary>
    /// Seçim dikdörtgenine kaynak merkezinden doku nakli (ofset klon).
    /// </summary>
    private static void ApplyFillRect(Image<Rgba32> image, TextureCloneOp op, NormalizedCropRect fill)
    {
        int w = image.Width;
        int h = image.Height;

        double left = Math.Clamp(fill.Left, 0, 1);
        double top = Math.Clamp(fill.Top, 0, 1);
        double right = Math.Clamp(fill.Left + fill.Width, 0, 1);
        double bottom = Math.Clamp(fill.Top + fill.Height, 0, 1);
        if (right - left < 0.001 || bottom - top < 0.001)
            return;

        int x0 = Math.Clamp((int)Math.Floor(left * w), 0, w - 1);
        int y0 = Math.Clamp((int)Math.Floor(top * h), 0, h - 1);
        int x1 = Math.Clamp((int)Math.Ceiling(right * w), x0 + 1, w);
        int y1 = Math.Clamp((int)Math.Ceiling(bottom * h), y0 + 1, h);
        int rw = x1 - x0;
        int rh = y1 - y0;

        float dxC = (float)(Math.Clamp(op.DestCenter.X, 0, 1) * (w - 1));
        float dyC = (float)(Math.Clamp(op.DestCenter.Y, 0, 1) * (h - 1));
        float sxC = (float)(Math.Clamp(op.SourceCenter.X, 0, 1) * (w - 1));
        float syC = (float)(Math.Clamp(op.SourceCenter.Y, 0, 1) * (h - 1));
        float offX = sxC - dxC;
        float offY = syC - dyC;

        // Kaynak ROI: hedef dikdörtgen + ofset
        int sx0 = Math.Clamp((int)Math.Floor(x0 + offX) - 2, 0, w - 1);
        int sy0 = Math.Clamp((int)Math.Floor(y0 + offY) - 2, 0, h - 1);
        int sx1 = Math.Clamp((int)Math.Ceiling(x1 + offX) + 2, sx0 + 1, w);
        int sy1 = Math.Clamp((int)Math.Ceiling(y1 + offY) + 2, sy0 + 1, h);
        int srw = sx1 - sx0;
        int srh = sy1 - sy0;

        var srcR = new float[srw * srh];
        var srcG = new float[srw * srh];
        var srcB = new float[srw * srh];
        var srcA = new float[srw * srh];
        var dstR = new float[rw * rh];
        var dstG = new float[rw * rh];
        var dstB = new float[rw * rh];
        var dstA = new float[rw * rh];

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < srh; y++)
            {
                var row = accessor.GetRowSpan(sy0 + y);
                for (int x = 0; x < srw; x++)
                {
                    int i = y * srw + x;
                    var p = row[sx0 + x];
                    srcR[i] = p.R; srcG[i] = p.G; srcB[i] = p.B; srcA[i] = p.A;
                }
            }
            for (int y = 0; y < rh; y++)
            {
                var row = accessor.GetRowSpan(y0 + y);
                for (int x = 0; x < rw; x++)
                {
                    int i = y * rw + x;
                    var p = row[x0 + x];
                    dstR[i] = p.R; dstG[i] = p.G; dstB[i] = p.B; dstA[i] = p.A;
                }
            }
        });

        float feather = Math.Max(1.5f, Math.Min(rw, rh) * 0.08f);
        var outR = (float[])dstR.Clone();
        var outG = (float[])dstG.Clone();
        var outB = (float[])dstB.Clone();
        var outA = (float[])dstA.Clone();

        for (int y = 0; y < rh; y++)
        {
            for (int x = 0; x < rw; x++)
            {
                float gx = x0 + x + 0.5f;
                float gy = y0 + y + 0.5f;
                float edgeDist = Math.Min(
                    Math.Min(gx - x0, x1 - gx),
                    Math.Min(gy - y0, y1 - gy));
                float blend = edgeDist >= feather
                    ? 1f
                    : SoftStep(Math.Clamp(edgeDist / feather, 0f, 1f));
                if (blend < 0.01f)
                    continue;

                float sx = gx + offX;
                float sy = gy + offY;
                if (!SampleBilinear(srcR, srcG, srcB, srcA, srw, srh, sx0, sy0, sx, sy,
                        out float sr, out float sg, out float sb, out float sa))
                    continue;

                int i = y * rw + x;
                outR[i] = dstR[i] * (1f - blend) + sr * blend;
                outG[i] = dstG[i] * (1f - blend) + sg * blend;
                outB[i] = dstB[i] * (1f - blend) + sb * blend;
                outA[i] = dstA[i] * (1f - blend) + sa * blend;
            }
        }

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < rh; y++)
            {
                var row = accessor.GetRowSpan(y0 + y);
                for (int x = 0; x < rw; x++)
                {
                    int i = y * rw + x;
                    row[x0 + x] = new Rgba32(
                        (byte)Math.Clamp(outR[i] + 0.5f, 0, 255),
                        (byte)Math.Clamp(outG[i] + 0.5f, 0, 255),
                        (byte)Math.Clamp(outB[i] + 0.5f, 0, 255),
                        (byte)Math.Clamp(outA[i] + 0.5f, 0, 255));
                }
            }
        });
    }

    /// <summary>
    /// Şekil mesafesi: çekirdek yarıçapıyla karşılaştırılabilir (dairede Öklid).
    /// </summary>
    private static float ShapeDistance(
        float x, float y, float cx, float cy, float radius, TextureCloneBrushShape shape)
    {
        float dx = x - cx;
        float dy = y - cy;
        return shape switch
        {
            TextureCloneBrushShape.Square or TextureCloneBrushShape.Normal =>
                Math.Max(Math.Abs(dx), Math.Abs(dy)),
            TextureCloneBrushShape.SoftSquare =>
                // Superellipse n=4 — yumuşak köşeli kare
                radius * MathF.Pow(
                    MathF.Pow(Math.Abs(dx) / Math.Max(1e-3f, radius), 4f)
                    + MathF.Pow(Math.Abs(dy) / Math.Max(1e-3f, radius), 4f),
                    0.25f),
            TextureCloneBrushShape.Ellipse =>
                radius * MathF.Sqrt(
                    (dx * dx) / Math.Max(1e-3f, radius * radius)
                    + (dy * dy) / Math.Max(1e-3f, (radius * 0.62f) * (radius * 0.62f))),
            _ => MathF.Sqrt(dx * dx + dy * dy)
        };
    }

    private static bool SampleBilinear(
        float[] r, float[] g, float[] b, float[] a,
        int rw, int rh, int ox, int oy,
        float gx, float gy,
        out float sr, out float sg, out float sb, out float sa)
    {
        float lx = gx - ox;
        float ly = gy - oy;

        if (lx < 0 || ly < 0 || lx > rw - 1 || ly > rh - 1)
        {
            int ix = (int)Math.Clamp(MathF.Round(lx), 0, rw - 1);
            int iy = (int)Math.Clamp(MathF.Round(ly), 0, rh - 1);
            int i = iy * rw + ix;
            sr = r[i]; sg = g[i]; sb = b[i]; sa = a[i];
            return lx >= -2 && ly >= -2 && lx <= rw + 1 && ly <= rh + 1;
        }

        int x0 = (int)MathF.Floor(lx);
        int y0 = (int)MathF.Floor(ly);
        x0 = Math.Clamp(x0, 0, rw - 2);
        y0 = Math.Clamp(y0, 0, rh - 2);
        float fx = Math.Clamp(lx - x0, 0f, 1f);
        float fy = Math.Clamp(ly - y0, 0f, 1f);

        int i00 = y0 * rw + x0;
        int i10 = i00 + 1;
        int i01 = i00 + rw;
        int i11 = i01 + 1;

        sr = Lerp(Lerp(r[i00], r[i10], fx), Lerp(r[i01], r[i11], fx), fy);
        sg = Lerp(Lerp(g[i00], g[i10], fx), Lerp(g[i01], g[i11], fx), fy);
        sb = Lerp(Lerp(b[i00], b[i10], fx), Lerp(b[i01], b[i11], fx), fy);
        sa = Lerp(Lerp(a[i00], a[i10], fx), Lerp(a[i01], a[i11], fx), fy);
        return true;
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static float SoftStep(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
