using RonekaiImageFramer.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

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

        // Kilitli PNG kesit: görünen seçimin bire bir kopyası
        if (op.ExactCopy && op.PatchPng is { Length: > 0 })
        {
            ApplyExactPatchPng(image, op);
            return;
        }

        // Çokgen seçim her zaman bire bir ExactCopy (yumuşak FillRect'e düşmesin)
        bool exact = op.ExactCopy || op.SourcePolygon is { Count: >= 3 };
        if (exact && op.SourceRect is { } srcExact)
        {
            ApplyExactCopy(image, op, srcExact);
            return;
        }

        if (op.FillRect is { } fill)
        {
            ApplyFillRect(image, op, fill);
            return;
        }

        ApplyBrushStamp(image, op);
    }

    /// <summary>Kaynak al ile kilitlenen PNG kesiti hedefe yapıştırır (isteğe bağlı damga açısı).</summary>
    private static void ApplyExactPatchPng(Image<Rgba32> image, TextureCloneOp op)
    {
        if (op.PatchPng is null || op.PatchPng.Length == 0)
            return;

        try
        {
            using var patch = Image.Load<Rgba32>(op.PatchPng);
            if (patch.Width < 1 || patch.Height < 1)
                return;

            float destCx = (float)(Math.Clamp(op.DestCenter.X, 0, 1) * (image.Width - 1));
            float destCy = (float)(Math.Clamp(op.DestCenter.Y, 0, 1) * (image.Height - 1));
            float stampRot = (float)op.RotationDegrees;

            // Hedef boyutu: SourceRect norm → mevcut tuval pikseli (önizleme/export boyutu farkı)
            int targetW = patch.Width;
            int targetH = patch.Height;
            if (op.SourceRect is { } sr)
            {
                targetW = Math.Max(1, (int)Math.Round(sr.Width * image.Width));
                targetH = Math.Max(1, (int)Math.Round(sr.Height * image.Height));
            }

            Image<Rgba32> stamp = patch;
            Image<Rgba32>? scaled = null;
            try
            {
                if (targetW != patch.Width || targetH != patch.Height)
                {
                    scaled = patch.Clone(ctx => ctx.Resize(new ResizeOptions
                    {
                        Size = new Size(targetW, targetH),
                        Mode = ResizeMode.Stretch,
                        Sampler = KnownResamplers.NearestNeighbor
                    }));
                    stamp = scaled;
                }

                // Kaynak centroid (SourceCenter) → hedef tık; AABB ortası değil
                // (yamuk seçimde AABB ≠ centroid; overlay ile aynı hizalama)
                float pcx = (stamp.Width - 1) / 2f;
                float pcy = (stamp.Height - 1) / 2f;
                if (op.SourceRect is { } srAlign && srAlign.Width > 1e-6 && srAlign.Height > 1e-6)
                {
                    double fx = (op.SourceCenter.X - srAlign.Left) / srAlign.Width;
                    double fy = (op.SourceCenter.Y - srAlign.Top) / srAlign.Height;
                    pcx = (float)(Math.Clamp(fx, 0, 1) * (stamp.Width - 1));
                    pcy = (float)(Math.Clamp(fy, 0, 1) * (stamp.Height - 1));
                }

                ApplyExactCopyFromPatchBuffer(image, stamp, destCx, destCy, stampRot, pcx, pcy);
            }
            finally
            {
                scaled?.Dispose();
            }
        }
        catch
        {
            // PNG bozuksa sessizce yoksay
        }
    }

    private static void ApplyExactCopyFromPatchBuffer(
        Image<Rgba32> dest, Image<Rgba32> patch, float destCx, float destCy, float degrees,
        float pcx, float pcy)
    {
        int w = dest.Width;
        int h = dest.Height;
        int pw = patch.Width;
        int ph = patch.Height;
        var buf = new Rgba32[pw * ph];
        patch.CopyPixelDataTo(buf);

        float rad = degrees * MathF.PI / 180f;
        float cos = MathF.Cos(rad);
        float sin = MathF.Sin(rad);

        float diag = MathF.Sqrt(pw * pw + ph * ph);
        int half = (int)Math.Ceiling(diag / 2f) + 2;
        int x0 = Math.Clamp((int)Math.Floor(destCx - half), 0, w);
        int y0 = Math.Clamp((int)Math.Floor(destCy - half), 0, h);
        int x1 = Math.Clamp((int)Math.Ceiling(destCx + half), x0, w);
        int y1 = Math.Clamp((int)Math.Ceiling(destCy + half), y0, h);

        dest.ProcessPixelRows(accessor =>
        {
            for (int dy = y0; dy < y1; dy++)
            {
                var row = accessor.GetRowSpan(dy);
                for (int dx = x0; dx < x1; dx++)
                {
                    float ox = dx + 0.5f - destCx;
                    float oy = dy + 0.5f - destCy;
                    float lx = ox * cos + oy * sin + pcx;
                    float ly = -ox * sin + oy * cos + pcy;
                    int sx = (int)MathF.Round(lx);
                    int sy = (int)MathF.Round(ly);
                    if ((uint)sx >= (uint)pw || (uint)sy >= (uint)ph)
                        continue;
                    var p = buf[sy * pw + sx];
                    if (p.A == 0)
                        continue;
                    row[dx] = p;
                }
            }
        });
    }

    /// <summary>
    /// Seçim alanını bire bir kopyalar. SourcePolygon varsa çokgen içi kopyalanır.
    /// RotationDegrees: centroid etrafında damgayı döndürür (overlay pin dönüşü ile aynı).
    /// </summary>
    private static void ApplyExactCopy(Image<Rgba32> image, TextureCloneOp op, NormalizedCropRect sourceRect)
    {
        int w = image.Width;
        int h = image.Height;
        if (w < 2 || h < 2)
            return;

        float NormX(double n) => (float)(Math.Clamp(n, 0, 1) * (w - 1));
        float NormY(double n) => (float)(Math.Clamp(n, 0, 1) * (h - 1));

        float destCx = NormX(op.DestCenter.X);
        float destCy = NormY(op.DestCenter.Y);
        float stampRot = (float)op.RotationDegrees;
        float rad = stampRot * MathF.PI / 180f;
        float cos = MathF.Cos(rad);
        float sin = MathF.Sin(rad);

        Vec2[]? srcPoly = null;
        if (op.SourcePolygon is { Count: >= 3 } srcPts)
        {
            var raw = srcPts.Select(p => new Vec2(NormX(p.X), NormY(p.Y))).ToArray();
            srcPoly = ConvexHull(raw);
            if (srcPoly.Length < 3)
                srcPoly = raw;
        }

        float srcCx, srcCy, srcLeft, srcTop, srcRight, srcBottom;
        if (srcPoly is { Length: >= 3 })
        {
            srcLeft = srcPoly.Min(p => p.X);
            srcTop = srcPoly.Min(p => p.Y);
            srcRight = srcPoly.Max(p => p.X);
            srcBottom = srcPoly.Max(p => p.Y);
            srcCx = srcPoly.Average(p => p.X);
            srcCy = srcPoly.Average(p => p.Y);
        }
        else
        {
            srcLeft = NormX(sourceRect.Left);
            srcTop = NormY(sourceRect.Top);
            srcRight = NormX(sourceRect.Left + sourceRect.Width);
            srcBottom = NormY(sourceRect.Top + sourceRect.Height);
            srcCx = (srcLeft + srcRight) * 0.5f;
            srcCy = (srcTop + srcBottom) * 0.5f;

            // Dikdörtgen / şekil: 4 köşe çokgeni
            srcPoly =
            [
                new Vec2(srcLeft, srcTop),
                new Vec2(srcRight, srcTop),
                new Vec2(srcRight, srcBottom),
                new Vec2(srcLeft, srcBottom)
            ];

            bool shaped = op.Shape is TextureCloneBrushShape.Circle
                or TextureCloneBrushShape.Ellipse
                or TextureCloneBrushShape.SoftSquare;
            if (shaped)
            {
                // Şekil maskesi: köşe çokgeni yerine mesafe testi (aşağıda)
                srcPoly = null;
            }
        }

        float rx = Math.Max(1f, (srcRight - srcLeft) * 0.5f);
        float ry = Math.Max(1f, (srcBottom - srcTop) * 0.5f);
        bool useShapeMask = srcPoly is null;

        // Hedef ROI: kaynak AABB köşelerini damga açısıyla çevir
        float minDx = float.MaxValue, minDy = float.MaxValue;
        float maxDx = float.MinValue, maxDy = float.MinValue;
        ReadOnlySpan<(float x, float y)> corners =
        [
            (srcLeft, srcTop), (srcRight, srcTop), (srcRight, srcBottom), (srcLeft, srcBottom)
        ];
        foreach (var (x, y) in corners)
        {
            float ox = x - srcCx;
            float oy = y - srcCy;
            float dx = destCx + ox * cos - oy * sin;
            float dy = destCy + ox * sin + oy * cos;
            minDx = Math.Min(minDx, dx);
            minDy = Math.Min(minDy, dy);
            maxDx = Math.Max(maxDx, dx);
            maxDy = Math.Max(maxDy, dy);
        }

        int x0 = Math.Clamp((int)Math.Floor(minDx) - 1, 0, w - 1);
        int y0 = Math.Clamp((int)Math.Floor(minDy) - 1, 0, h - 1);
        int x1 = Math.Clamp((int)Math.Ceiling(maxDx) + 1, x0 + 1, w);
        int y1 = Math.Clamp((int)Math.Ceiling(maxDy) + 1, y0 + 1, h);

        // Kaynak piksellerini oku (ROI)
        int sx0 = Math.Clamp((int)Math.Floor(srcLeft) - 1, 0, w - 1);
        int sy0 = Math.Clamp((int)Math.Floor(srcTop) - 1, 0, h - 1);
        int sx1 = Math.Clamp((int)Math.Ceiling(srcRight) + 1, sx0 + 1, w);
        int sy1 = Math.Clamp((int)Math.Ceiling(srcBottom) + 1, sy0 + 1, h);
        int srw = sx1 - sx0;
        int srh = sy1 - sy0;
        var srcBuf = new Rgba32[srw * srh];
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < srh; y++)
            {
                var row = accessor.GetRowSpan(sy0 + y);
                for (int x = 0; x < srw; x++)
                    srcBuf[y * srw + x] = row[sx0 + x];
            }
        });

        // Overlay ile aynı: dest = destC + R*(src - srcC)
        // Örnekleme: src = srcC + R^T*(dest - destC)
        image.ProcessPixelRows(accessor =>
        {
            for (int dy = y0; dy < y1; dy++)
            {
                var destRow = accessor.GetRowSpan(dy);
                for (int dx = x0; dx < x1; dx++)
                {
                    float ox = dx + 0.5f - destCx;
                    float oy = dy + 0.5f - destCy;
                    float lx = ox * cos + oy * sin;
                    float ly = -ox * sin + oy * cos;
                    float sx = lx + srcCx;
                    float sy = ly + srcCy;

                    bool inside;
                    if (srcPoly is not null)
                        inside = PointInPolygon(sx, sy, srcPoly);
                    else if (useShapeMask)
                        inside = ShapeDistanceRect(sx, sy, srcCx, srcCy, rx, ry, op.Shape, 0) <= 1f;
                    else
                        inside = sx >= srcLeft && sx <= srcRight && sy >= srcTop && sy <= srcBottom;

                    if (!inside)
                        continue;

                    int ix = (int)MathF.Floor(sx) - sx0;
                    int iy = (int)MathF.Floor(sy) - sy0;
                    if ((uint)ix >= (uint)srw || (uint)iy >= (uint)srh)
                        continue;

                    var p = srcBuf[iy * srw + ix];
                    destRow[dx] = new Rgba32(p.R, p.G, p.B, p.A == 0 ? (byte)255 : p.A);
                }
            }
        });
    }

    private static NormalizedPoint[] RotatedRectCorners(NormalizedCropRect rect, float rotDeg)
    {
        double cx = rect.Left + rect.Width * 0.5;
        double cy = rect.Top + rect.Height * 0.5;
        double hw = rect.Width * 0.5;
        double hh = rect.Height * 0.5;
        double rad = rotDeg * Math.PI / 180.0;
        double cos = Math.Cos(rad);
        double sin = Math.Sin(rad);
        (double ox, double oy)[] local = [(-hw, -hh), (hw, -hh), (hw, hh), (-hw, hh)];
        var corners = new NormalizedPoint[4];
        for (int i = 0; i < 4; i++)
        {
            double x = cx + local[i].ox * cos - local[i].oy * sin;
            double y = cy + local[i].ox * sin + local[i].oy * cos;
            corners[i] = new NormalizedPoint(x, y);
        }
        return corners;
    }

    /// <summary>1 = kenar; şekil içinde ≤ 1.</summary>
    private static float ShapeDistanceRect(
        float x, float y, float cx, float cy, float rx, float ry,
        TextureCloneBrushShape shape, float rotationDeg)
    {
        float dx = x - cx;
        float dy = y - cy;
        if (Math.Abs(rotationDeg) > 0.01f)
        {
            float rad = -rotationDeg * MathF.PI / 180f;
            float cos = MathF.Cos(rad);
            float sin = MathF.Sin(rad);
            float rxp = dx * cos - dy * sin;
            float ryp = dx * sin + dy * cos;
            dx = rxp;
            dy = ryp;
        }

        float nx = dx / Math.Max(1e-3f, rx);
        float ny = dy / Math.Max(1e-3f, ry);
        return shape switch
        {
            TextureCloneBrushShape.Ellipse or TextureCloneBrushShape.Circle =>
                MathF.Sqrt(nx * nx + ny * ny),
            TextureCloneBrushShape.SoftSquare =>
                MathF.Pow(MathF.Pow(MathF.Abs(nx), 4f) + MathF.Pow(MathF.Abs(ny), 4f), 0.25f),
            _ => Math.Max(MathF.Abs(nx), MathF.Abs(ny))
        };
    }

    private readonly record struct Vec2(float X, float Y);

    /// <summary>Dışbükey çokgen: kenar çapraz çarpımları aynı işaretli.</summary>
    private static bool PointInConvexPolygon(float x, float y, Vec2[] poly)
    {
        if (poly.Length < 3)
            return false;

        bool? pos = null;
        for (int i = 0; i < poly.Length; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % poly.Length];
            float cross = (b.X - a.X) * (y - a.Y) - (b.Y - a.Y) * (x - a.X);
            if (Math.Abs(cross) < 1e-4f)
                continue;
            bool p = cross > 0;
            if (pos is null)
                pos = p;
            else if (pos != p)
                return false;
        }

        return true;
    }

    /// <summary>Monotone chain dışbükey zarf (CCW). Bowtie pin sırasını düzeltir.</summary>
    private static Vec2[] ConvexHull(Vec2[] points)
    {
        if (points.Length <= 3)
            return points;

        var pts = points
            .OrderBy(p => p.X)
            .ThenBy(p => p.Y)
            .ToArray();

        static float Cross(Vec2 o, Vec2 a, Vec2 b) =>
            (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);

        var lower = new List<Vec2>(pts.Length);
        foreach (var p in pts)
        {
            while (lower.Count >= 2 && Cross(lower[^2], lower[^1], p) <= 0)
                lower.RemoveAt(lower.Count - 1);
            lower.Add(p);
        }

        var upper = new List<Vec2>(pts.Length);
        for (int i = pts.Length - 1; i >= 0; i--)
        {
            var p = pts[i];
            while (upper.Count >= 2 && Cross(upper[^2], upper[^1], p) <= 0)
                upper.RemoveAt(upper.Count - 1);
            upper.Add(p);
        }

        lower.RemoveAt(lower.Count - 1);
        upper.RemoveAt(upper.Count - 1);
        lower.AddRange(upper);
        return lower.Count >= 3 ? lower.ToArray() : points;
    }

    private static bool PointInPolygon(float x, float y, Vec2[] poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            float xi = poly[i].X, yi = poly[i].Y;
            float xj = poly[j].X, yj = poly[j].Y;
            bool intersect = ((yi > y) != (yj > y))
                             && (x < (xj - xi) * (y - yi) / Math.Max(1e-6f, yj - yi) + xi);
            if (intersect)
                inside = !inside;
        }
        return inside;
    }

    private static void ApplyBrushStamp(Image<Rgba32> image, TextureCloneOp op)
    {
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
        float rotationDeg = (float)op.RotationDegrees;

        // Elips / kare / dönüş için ROI
        float roiR = shape switch
        {
            TextureCloneBrushShape.Ellipse => Math.Max(outerR, outerR * 0.62f) + 1f,
            TextureCloneBrushShape.Square or TextureCloneBrushShape.SoftSquare or TextureCloneBrushShape.Normal
                => outerR + 1f,
            _ => outerR + 1f
        };
        if (Math.Abs(rotationDeg) > 0.01f)
            roiR *= 1.42f; // köşegen payı

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

                float dd = ShapeDistance(gx, gy, dxC, dyC, radius, shape, rotationDeg);
                if (dd >= ringInner && dd <= outerR)
                {
                    dstSumR += bufR[i]; dstSumG += bufG[i]; dstSumB += bufB[i];
                    dstCount++;
                }

                float sd = ShapeDistance(gx, gy, sxC, syC, radius, shape, rotationDeg);
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
                float dist = ShapeDistance(gx, gy, dxC, dyC, radius, shape, rotationDeg);
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
    /// rotationDeg: şekli saat yönünde döndürür (nokta ters çevrilerek uzaya alınır).
    /// </summary>
    private static float ShapeDistance(
        float x, float y, float cx, float cy, float radius, TextureCloneBrushShape shape,
        float rotationDeg = 0)
    {
        float dx = x - cx;
        float dy = y - cy;
        if (Math.Abs(rotationDeg) > 0.01f)
        {
            float rad = -rotationDeg * MathF.PI / 180f;
            float cos = MathF.Cos(rad);
            float sin = MathF.Sin(rad);
            float rx = dx * cos - dy * sin;
            float ry = dx * sin + dy * cos;
            dx = rx;
            dy = ry;
        }

        return shape switch
        {
            TextureCloneBrushShape.Square or TextureCloneBrushShape.Normal =>
                Math.Max(Math.Abs(dx), Math.Abs(dy)),
            TextureCloneBrushShape.SoftSquare =>
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
