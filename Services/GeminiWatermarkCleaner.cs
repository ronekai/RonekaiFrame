using RonekaiImageFramer.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace RonekaiImageFramer.Services;

/// <summary>
/// Pin/dikdörtgen seçiminin içini doldurur.
/// Zemin tonu: yerel dış halka + kenardan içeri yayılım (tek düz renk değil).
/// Bulut/duman ≈ geniş yumuşak geçiş; Blok ≈ daha net.
/// </summary>
public static class GeminiWatermarkCleaner
{
    public static void ApplyAll(Image<Rgba32> image, IReadOnlyList<WatermarkCleanOp> ops, bool previewFast = false)
    {
        if (ops is null || ops.Count == 0)
            return;
        foreach (var op in ops)
            Apply(image, op, previewFast);
    }

    public static void Apply(Image<Rgba32> image, WatermarkCleanOp op, bool previewFast = false)
    {
        if (op.IsBrush && op.BrushCenter is { } center)
        {
            ApplyBrush(image, center, op.BrushRadiusNorm, op.BrushShape, op.Style, previewFast, op.RotationDegrees);
            return;
        }

        if (op.Polygon is null || op.Polygon.Count == 0)
            return;
        ApplyPolygon(image, op.Polygon, op.Style, previewFast);
    }

    /// <summary>Daire/kare/elips fırça maskesini çokgene çevirip temizler.</summary>
    public static void ApplyBrush(
        Image<Rgba32> image,
        NormalizedPoint center,
        double radiusNorm,
        TextureCloneBrushShape shape,
        WatermarkCleanStyle style,
        bool previewFast = false,
        double rotationDegrees = 0)
    {
        if (image.Width < 8 || image.Height < 8)
            return;

        float shortEdge = Math.Min(image.Width, image.Height);
        float radius = Math.Max(1.5f, (float)Math.Clamp(radiusNorm, 0.002, 0.35) * shortEdge);
        float cx = (float)(Math.Clamp(center.X, 0, 1) * (image.Width - 1));
        float cy = (float)(Math.Clamp(center.Y, 0, 1) * (image.Height - 1));
        float rotRad = (float)(rotationDegrees * Math.PI / 180.0);
        float cos = MathF.Cos(rotRad);
        float sin = MathF.Sin(rotRad);

        // Şekli kapalı çokgen olarak örnekle (cleaner mevcut çokgen yolunu kullanır)
        int samples = shape is TextureCloneBrushShape.Square or TextureCloneBrushShape.Normal ? 4 : 32;
        var poly = new List<NormalizedPoint>(samples);
        if (shape is TextureCloneBrushShape.Square or TextureCloneBrushShape.Normal)
        {
            float r = radius;
            ReadOnlySpan<(float dx, float dy)> corners =
            [
                (-r, -r), (r, -r), (r, r), (-r, r)
            ];
            foreach (var (dx, dy) in corners)
            {
                float rx = dx * cos - dy * sin;
                float ry = dx * sin + dy * cos;
                poly.Add(Norm(image, cx + rx, cy + ry));
            }
        }
        else
        {
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples * MathF.PI * 2f;
                float dx, dy;
                if (shape == TextureCloneBrushShape.Ellipse)
                {
                    dx = MathF.Cos(t) * radius;
                    dy = MathF.Sin(t) * radius * 0.62f;
                }
                else if (shape == TextureCloneBrushShape.SoftSquare)
                {
                    float c = MathF.Cos(t);
                    float s = MathF.Sin(t);
                    float ax = MathF.Pow(MathF.Abs(c), 0.5f) * MathF.Sign(c);
                    float ay = MathF.Pow(MathF.Abs(s), 0.5f) * MathF.Sign(s);
                    dx = ax * radius;
                    dy = ay * radius;
                }
                else
                {
                    dx = MathF.Cos(t) * radius;
                    dy = MathF.Sin(t) * radius;
                }

                float rx = dx * cos - dy * sin;
                float ry = dx * sin + dy * cos;
                poly.Add(Norm(image, cx + rx, cy + ry));
            }
        }

        ApplyPolygon(image, poly, style, previewFast);
    }

    private static NormalizedPoint Norm(Image<Rgba32> image, float x, float y)
    {
        double nx = image.Width <= 1 ? 0 : Math.Clamp(x / (image.Width - 1), 0, 1);
        double ny = image.Height <= 1 ? 0 : Math.Clamp(y / (image.Height - 1), 0, 1);
        return new NormalizedPoint(nx, ny);
    }

    public static void Apply(
        Image<Rgba32> image,
        NormalizedCropRect region,
        WatermarkCleanStyle style = WatermarkCleanStyle.Cloud)
    {
        NormalizedPoint[] poly =
        [
            new(region.Left, region.Top),
            new(region.Left + region.Width, region.Top),
            new(region.Left + region.Width, region.Top + region.Height),
            new(region.Left, region.Top + region.Height)
        ];
        ApplyPolygon(image, poly, style, previewFast: false);
    }

    public static void ApplyPolygon(
        Image<Rgba32> image,
        IReadOnlyList<NormalizedPoint> polygon,
        WatermarkCleanStyle style = WatermarkCleanStyle.Cloud,
        bool previewFast = false)
    {
        if (image.Width < 8 || image.Height < 8 || polygon.Count == 0)
            return;

        int imgW = image.Width;
        int imgH = image.Height;
        var pts = polygon
            .Select(p => new Vec2(
                (float)(Math.Clamp(p.X, 0, 1) * (imgW - 1)),
                (float)(Math.Clamp(p.Y, 0, 1) * (imgH - 1))))
            .ToArray();

        float minX = pts.Min(p => p.X);
        float minY = pts.Min(p => p.Y);
        float maxX = pts.Max(p => p.X);
        float maxY = pts.Max(p => p.Y);

        float lineHalf = Math.Max(12f, Math.Min(imgW, imgH) * 0.024f);
        if (pts.Length == 2)
        {
            minX -= lineHalf * 2;
            minY -= lineHalf * 2;
            maxX += lineHalf * 2;
            maxY += lineHalf * 2;
        }

        var tune = GetStyleTune(style);
        float shortEdge = Math.Min(imgW, imgH);
        float regionShort = Math.Max(1f, Math.Min(maxX - minX, maxY - minY));
        float outerFeather = Math.Max(tune.OuterFeatherMin, shortEdge * tune.OuterFeatherEdgeFrac);
        float innerSoft = tune.InnerFromRegion
            ? Math.Max(tune.InnerSoftMin, regionShort * tune.InnerSoftRegionFrac)
            : Math.Max(tune.InnerSoftMin, shortEdge * tune.InnerSoftEdgeFrac);

        // Dış halka: filigram kenarından biraz uzak, gerçek zeminden örnekle
        float ringInner = Math.Max(2f, outerFeather * 0.25f);
        float ringOuter = Math.Max(ringInner + 4f, outerFeather * tune.RingOuterMul);

        int pad = (int)Math.Ceiling(Math.Max(ringOuter, lineHalf)) + 10;
        int x0 = Math.Clamp((int)Math.Floor(minX) - pad, 0, imgW - 1);
        int y0 = Math.Clamp((int)Math.Floor(minY) - pad, 0, imgH - 1);
        int x1 = Math.Clamp((int)Math.Ceiling(maxX) + pad, x0 + 1, imgW);
        int y1 = Math.Clamp((int)Math.Ceiling(maxY) + pad, y0 + 1, imgH);
        int rw = x1 - x0;
        int rh = y1 - y0;
        if (rw < 2 || rh < 2)
            return;

        int n = rw * rh;
        var inside = new bool[n];
        var srcR = new float[n];
        var srcG = new float[n];
        var srcB = new float[n];
        var srcA = new float[n];
        var edgeDist = new float[n];

        var ring = new List<(float x, float y, float r, float g, float b, float a)>(512);

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < rh; y++)
            {
                var row = accessor.GetRowSpan(y0 + y);
                for (int x = 0; x < rw; x++)
                {
                    int i = y * rw + x;
                    var px = row[x0 + x];
                    srcR[i] = px.R;
                    srcG[i] = px.G;
                    srcB[i] = px.B;
                    srcA[i] = px.A;

                    float gx = x0 + x + 0.5f;
                    float gy = y0 + y + 0.5f;
                    bool isIn = IsInsideShape(gx, gy, pts, lineHalf);
                    inside[i] = isIn;
                    edgeDist[i] = DistanceToShapeBoundary(gx, gy, pts, lineHalf);

                    if (!isIn && edgeDist[i] >= ringInner && edgeDist[i] <= ringOuter)
                    {
                        // Önizlemede halkayı seyrelt — IDW maliyetini düşür
                        if (previewFast && ((x + y) & 1) != 0)
                            continue;
                        ring.Add((x + 0.5f, y + 0.5f, px.R, px.G, px.B, px.A));
                    }
                }
            }
        });

        if (ring.Count == 0)
        {
            for (int i = 0; i < n; i++)
            {
                if (inside[i])
                    continue;
                int x = i % rw;
                int y = i / rw;
                ring.Add((x + 0.5f, y + 0.5f, srcR[i], srcG[i], srcB[i], srcA[i]));
            }
        }

        if (ring.Count == 0)
            return;

        // IDW maliyetini düşür — çok kalabalık halkayı seyrelt
        if (ring.Count > 160)
        {
            int step = Math.Max(1, ring.Count / 160);
            var thinned = new List<(float x, float y, float r, float g, float b, float a)>(160);
            for (int i = 0; i < ring.Count; i += step)
                thinned.Add(ring[i]);
            ring = thinned;
        }

        // Robust ortalama (medyan) — tek düz renk yedek
        float meanR = Median(ring.Select(s => s.r));
        float meanG = Median(ring.Select(s => s.g));
        float meanB = Median(ring.Select(s => s.b));
        float meanA = Median(ring.Select(s => s.a));

        var bufR = new float[n];
        var bufG = new float[n];
        var bufB = new float[n];
        var bufA = new float[n];

        // 1) Dışı orijinal; içi yerel IDW zemin tahmini
        float idwRadius = Math.Max(24f, Math.Max(rw, rh) * 0.55f);
        float idwRadius2 = idwRadius * idwRadius;

        for (int i = 0; i < n; i++)
        {
            if (!inside[i])
            {
                bufR[i] = srcR[i];
                bufG[i] = srcG[i];
                bufB[i] = srcB[i];
                bufA[i] = srcA[i];
                continue;
            }

            float px = (i % rw) + 0.5f;
            float py = (i / rw) + 0.5f;
            SampleLocalGround(ring, px, py, idwRadius2, meanR, meanG, meanB, meanA,
                out float lr, out float lg, out float lb, out float la);
            bufR[i] = lr;
            bufG[i] = lg;
            bufB[i] = lb;
            bufA[i] = la;
        }

        // 2) Kenardan içeri Laplace yayılım — gradientli zemini sürdürür
        int side = Math.Max(rw, rh);
        int diffusePasses = previewFast
            ? Math.Clamp(side / tune.DiffusePreviewDiv, tune.DiffusePreviewMin, tune.DiffusePreviewMax)
            : Math.Clamp(side / tune.DiffuseExportDiv, tune.DiffuseExportMin, tune.DiffuseExportMax);
        DiffuseInterior(bufR, bufG, bufB, bufA, inside, rw, rh, diffusePasses);

        // 3) Bulanık / duman geçişi
        int blurPasses = previewFast
            ? Math.Clamp(side / tune.BlurPreviewDiv, tune.BlurPreviewMin, tune.BlurPreviewMax)
            : Math.Clamp(side / tune.BlurExportDiv, tune.BlurExportMin, tune.BlurExportMax);
        BoxBlur(bufR, bufG, bufB, bufA, rw, rh, blurPasses);

        // Blur seçim dışına taşmasın — aksi halde "içini kırp" çevreyi bozar
        RestoreOutside(bufR, bufG, bufB, bufA, srcR, srcG, srcB, srcA, inside, n);

        if (tune.SoftFeatherPass)
        {
            for (int i = 0; i < n; i++)
            {
                if (inside[i])
                    continue;
                float keep = SoftStep(Math.Clamp(edgeDist[i] / Math.Max(1f, outerFeather * 0.65f), 0f, 1f));
                bufR[i] = bufR[i] * (1f - keep) + srcR[i] * keep;
                bufG[i] = bufG[i] * (1f - keep) + srcG[i] * keep;
                bufB[i] = bufB[i] * (1f - keep) + srcB[i] * keep;
                bufA[i] = bufA[i] * (1f - keep) + srcA[i] * keep;
            }
            BoxBlur(bufR, bufG, bufB, bufA, rw, rh, previewFast ? Math.Max(2, blurPasses / 2) : Math.Max(3, blurPasses / 3));
            // İkinci blur sonrası da seçim dışı koru; yumuşak geçiş yalnızca iç + feather bandında kalsın
            RestoreOutsideBeyondFeather(
                bufR, bufG, bufB, bufA, srcR, srcG, srcB, srcA, inside, edgeDist, outerFeather, n);
        }

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < rh; y++)
            {
                var row = accessor.GetRowSpan(y0 + y);
                for (int x = 0; x < rw; x++)
                {
                    int i = y * rw + x;

                    // Net stillerde (TextureFill / SharpEdge / Block) seçim dışına hiç yazma
                    if (!tune.SoftFeatherPass && !inside[i])
                        continue;

                    float signed = inside[i] ? edgeDist[i] : -edgeDist[i];
                    float t = (signed + outerFeather) / (outerFeather + innerSoft);
                    float blend = SoftStep(Math.Clamp(t, 0f, 1f));

                    if (tune.ExtraSmoothBlend)
                        blend = blend * blend * (3f - 2f * blend);

                    if (blend < 0.01f)
                        continue;

                    if (tune.HardInsideBoost && inside[i])
                        blend = Math.Max(blend, 0.92f);
                    if (tune.HardInsideBoost && !inside[i] && blend < 0.15f)
                        continue;

                    ref var p = ref row[x0 + x];
                    p.R = (byte)Math.Clamp(srcR[i] * (1f - blend) + bufR[i] * blend + 0.5f, 0, 255);
                    p.G = (byte)Math.Clamp(srcG[i] * (1f - blend) + bufG[i] * blend + 0.5f, 0, 255);
                    p.B = (byte)Math.Clamp(srcB[i] * (1f - blend) + bufB[i] * blend + 0.5f, 0, 255);
                    p.A = (byte)Math.Clamp(srcA[i] * (1f - blend) + bufA[i] * blend + 0.5f, 0, 255);
                }
            }
        });
    }

    private static void RestoreOutside(
        float[] bufR, float[] bufG, float[] bufB, float[] bufA,
        float[] srcR, float[] srcG, float[] srcB, float[] srcA,
        bool[] inside, int n)
    {
        for (int i = 0; i < n; i++)
        {
            if (inside[i])
                continue;
            bufR[i] = srcR[i];
            bufG[i] = srcG[i];
            bufB[i] = srcB[i];
            bufA[i] = srcA[i];
        }
    }

    private static void RestoreOutsideBeyondFeather(
        float[] bufR, float[] bufG, float[] bufB, float[] bufA,
        float[] srcR, float[] srcG, float[] srcB, float[] srcA,
        bool[] inside, float[] edgeDist, float outerFeather, int n)
    {
        for (int i = 0; i < n; i++)
        {
            if (inside[i] || edgeDist[i] <= outerFeather)
                continue;
            bufR[i] = srcR[i];
            bufG[i] = srcG[i];
            bufB[i] = srcB[i];
            bufA[i] = srcA[i];
        }
    }

    private readonly record struct StyleTune(
        float OuterFeatherMin,
        float OuterFeatherEdgeFrac,
        float InnerSoftMin,
        float InnerSoftRegionFrac,
        float InnerSoftEdgeFrac,
        bool InnerFromRegion,
        float RingOuterMul,
        int DiffusePreviewDiv,
        int DiffuseExportDiv,
        int DiffusePreviewMin,
        int DiffusePreviewMax,
        int DiffuseExportMin,
        int DiffuseExportMax,
        int BlurPreviewDiv,
        int BlurExportDiv,
        int BlurPreviewMin,
        int BlurPreviewMax,
        int BlurExportMin,
        int BlurExportMax,
        bool SoftFeatherPass,
        bool HardInsideBoost,
        bool ExtraSmoothBlend);

    private static StyleTune GetStyleTune(WatermarkCleanStyle style) => style switch
    {
        WatermarkCleanStyle.Block => new(
            OuterFeatherMin: 2f, OuterFeatherEdgeFrac: 0.004f,
            InnerSoftMin: 1f, InnerSoftRegionFrac: 0, InnerSoftEdgeFrac: 0.002f, InnerFromRegion: false,
            RingOuterMul: 1.4f,
            DiffusePreviewDiv: 8, DiffuseExportDiv: 5,
            DiffusePreviewMin: 4, DiffusePreviewMax: 12, DiffuseExportMin: 8, DiffuseExportMax: 28,
            BlurPreviewDiv: 999, BlurExportDiv: 999,
            BlurPreviewMin: 1, BlurPreviewMax: 1, BlurExportMin: 2, BlurExportMax: 2,
            SoftFeatherPass: false, HardInsideBoost: true, ExtraSmoothBlend: false),

        WatermarkCleanStyle.SharpEdge => new(
            OuterFeatherMin: 1f, OuterFeatherEdgeFrac: 0.002f,
            InnerSoftMin: 1f, InnerSoftRegionFrac: 0, InnerSoftEdgeFrac: 0.001f, InnerFromRegion: false,
            RingOuterMul: 1.2f,
            DiffusePreviewDiv: 10, DiffuseExportDiv: 6,
            DiffusePreviewMin: 3, DiffusePreviewMax: 10, DiffuseExportMin: 6, DiffuseExportMax: 20,
            BlurPreviewDiv: 999, BlurExportDiv: 999,
            BlurPreviewMin: 0, BlurPreviewMax: 1, BlurExportMin: 1, BlurExportMax: 1,
            SoftFeatherPass: false, HardInsideBoost: true, ExtraSmoothBlend: false),

        WatermarkCleanStyle.TextureFill => new(
            OuterFeatherMin: 6f, OuterFeatherEdgeFrac: 0.012f,
            InnerSoftMin: 4f, InnerSoftRegionFrac: 0.18f, InnerSoftEdgeFrac: 0, InnerFromRegion: true,
            RingOuterMul: 2.6f,
            DiffusePreviewDiv: 10, DiffuseExportDiv: 6,
            DiffusePreviewMin: 4, DiffusePreviewMax: 14, DiffuseExportMin: 8, DiffuseExportMax: 24,
            BlurPreviewDiv: 40, BlurExportDiv: 28,
            BlurPreviewMin: 1, BlurPreviewMax: 3, BlurExportMin: 2, BlurExportMax: 5,
            SoftFeatherPass: false, HardInsideBoost: false, ExtraSmoothBlend: false),

        WatermarkCleanStyle.SoftHeal => new(
            OuterFeatherMin: 22f, OuterFeatherEdgeFrac: 0.055f,
            InnerSoftMin: 16f, InnerSoftRegionFrac: 0.55f, InnerSoftEdgeFrac: 0, InnerFromRegion: true,
            RingOuterMul: 2.5f,
            DiffusePreviewDiv: 5, DiffuseExportDiv: 2,
            DiffusePreviewMin: 8, DiffusePreviewMax: 28, DiffuseExportMin: 14, DiffuseExportMax: 56,
            BlurPreviewDiv: 14, BlurExportDiv: 8,
            BlurPreviewMin: 4, BlurPreviewMax: 12, BlurExportMin: 8, BlurExportMax: 24,
            SoftFeatherPass: true, HardInsideBoost: false, ExtraSmoothBlend: true),

        WatermarkCleanStyle.DeepBlur => new(
            OuterFeatherMin: 28f, OuterFeatherEdgeFrac: 0.07f,
            InnerSoftMin: 20f, InnerSoftRegionFrac: 0.65f, InnerSoftEdgeFrac: 0, InnerFromRegion: true,
            RingOuterMul: 2.8f,
            DiffusePreviewDiv: 4, DiffuseExportDiv: 2,
            DiffusePreviewMin: 10, DiffusePreviewMax: 32, DiffuseExportMin: 16, DiffuseExportMax: 64,
            BlurPreviewDiv: 10, BlurExportDiv: 6,
            BlurPreviewMin: 6, BlurPreviewMax: 16, BlurExportMin: 10, BlurExportMax: 28,
            SoftFeatherPass: true, HardInsideBoost: false, ExtraSmoothBlend: true),

        WatermarkCleanStyle.Seamless => new(
            OuterFeatherMin: 16f, OuterFeatherEdgeFrac: 0.04f,
            InnerSoftMin: 12f, InnerSoftRegionFrac: 0.4f, InnerSoftEdgeFrac: 0, InnerFromRegion: true,
            RingOuterMul: 2.4f,
            DiffusePreviewDiv: 6, DiffuseExportDiv: 3,
            DiffusePreviewMin: 6, DiffusePreviewMax: 24, DiffuseExportMin: 12, DiffuseExportMax: 48,
            BlurPreviewDiv: 14, BlurExportDiv: 9,
            BlurPreviewMin: 3, BlurPreviewMax: 10, BlurExportMin: 6, BlurExportMax: 18,
            SoftFeatherPass: true, HardInsideBoost: false, ExtraSmoothBlend: true),

        // Cloud (default)
        _ => new(
            OuterFeatherMin: 18f, OuterFeatherEdgeFrac: 0.045f,
            InnerSoftMin: 14f, InnerSoftRegionFrac: 0.45f, InnerSoftEdgeFrac: 0, InnerFromRegion: true,
            RingOuterMul: 2.2f,
            DiffusePreviewDiv: 6, DiffuseExportDiv: 3,
            DiffusePreviewMin: 6, DiffusePreviewMax: 24, DiffuseExportMin: 12, DiffuseExportMax: 48,
            BlurPreviewDiv: 16, BlurExportDiv: 10,
            BlurPreviewMin: 3, BlurPreviewMax: 10, BlurExportMin: 6, BlurExportMax: 20,
            SoftFeatherPass: true, HardInsideBoost: false, ExtraSmoothBlend: true)
    };

    /// <summary>
    /// Yakındaki dış halka piksellerinden ters-mesafe ağırlıklı yerel zemin rengi.
    /// </summary>
    private static void SampleLocalGround(
        List<(float x, float y, float r, float g, float b, float a)> ring,
        float px, float py, float radius2,
        float fallbackR, float fallbackG, float fallbackB, float fallbackA,
        out float r, out float g, out float b, out float a)
    {
        float wr = 0, wg = 0, wb = 0, wa = 0, wSum = 0;
        int used = 0;

        for (int i = 0; i < ring.Count; i++)
        {
            var s = ring[i];
            float dx = s.x - px;
            float dy = s.y - py;
            float d2 = dx * dx + dy * dy;
            if (d2 > radius2)
                continue;

            // 1/(d²+ε) — yakındakiler baskın
            float w = 1f / (d2 + 4f);
            wr += s.r * w;
            wg += s.g * w;
            wb += s.b * w;
            wa += s.a * w;
            wSum += w;
            used++;
        }

        if (used < 3 || wSum < 1e-6f)
        {
            // En yakın birkaç örnek
            float best1 = float.MaxValue, best2 = float.MaxValue, best3 = float.MaxValue;
            int i1 = -1, i2 = -1, i3 = -1;
            for (int i = 0; i < ring.Count; i++)
            {
                float dx = ring[i].x - px;
                float dy = ring[i].y - py;
                float d2 = dx * dx + dy * dy;
                if (d2 < best1) { best3 = best2; i3 = i2; best2 = best1; i2 = i1; best1 = d2; i1 = i; }
                else if (d2 < best2) { best3 = best2; i3 = i2; best2 = d2; i2 = i; }
                else if (d2 < best3) { best3 = d2; i3 = i; }
            }

            wr = wg = wb = wa = wSum = 0;
            void Acc(int idx, float d2)
            {
                if (idx < 0) return;
                float w = 1f / (d2 + 4f);
                var s = ring[idx];
                wr += s.r * w; wg += s.g * w; wb += s.b * w; wa += s.a * w;
                wSum += w;
            }
            Acc(i1, best1);
            Acc(i2, best2);
            Acc(i3, best3);

            if (wSum < 1e-6f)
            {
                r = fallbackR; g = fallbackG; b = fallbackB; a = fallbackA;
                return;
            }
        }

        r = wr / wSum;
        g = wg / wSum;
        b = wb / wSum;
        a = wa / wSum;
    }

    /// <summary>
    /// İç pikselleri 4-komşu ortalamasıyla yumuşatır; dış sabit kalır → sınır tonu içeri akar.
    /// </summary>
    private static void DiffuseInterior(
        float[] r, float[] g, float[] b, float[] a,
        bool[] inside, int rw, int rh, int passes)
    {
        var tr = new float[r.Length];
        var tg = new float[g.Length];
        var tb = new float[b.Length];
        var ta = new float[a.Length];

        for (int pass = 0; pass < passes; pass++)
        {
            Array.Copy(r, tr, r.Length);
            Array.Copy(g, tg, g.Length);
            Array.Copy(b, tb, b.Length);
            Array.Copy(a, ta, a.Length);

            for (int y = 0; y < rh; y++)
            {
                for (int x = 0; x < rw; x++)
                {
                    int i = y * rw + x;
                    if (!inside[i])
                        continue;

                    float sr = 0, sg = 0, sb = 0, sa = 0, w = 0;
                    void Add(int nx, int ny)
                    {
                        if ((uint)nx >= (uint)rw || (uint)ny >= (uint)rh)
                            return;
                        int ni = ny * rw + nx;
                        sr += tr[ni]; sg += tg[ni]; sb += tb[ni]; sa += ta[ni];
                        w += 1f;
                    }

                    Add(x - 1, y);
                    Add(x + 1, y);
                    Add(x, y - 1);
                    Add(x, y + 1);

                    if (w < 1f)
                        continue;

                    // Eski değerle hafif karışım → daha stabil, aşırı düzleşme yok
                    float nw = 0.72f;
                    float ow = 1f - nw;
                    r[i] = ow * tr[i] + nw * (sr / w);
                    g[i] = ow * tg[i] + nw * (sg / w);
                    b[i] = ow * tb[i] + nw * (sb / w);
                    a[i] = ow * ta[i] + nw * (sa / w);
                }
            }
        }
    }

    private static float Median(IEnumerable<float> values)
    {
        var arr = values.ToArray();
        if (arr.Length == 0)
            return 0;
        Array.Sort(arr);
        int m = arr.Length / 2;
        return arr.Length % 2 == 0 ? (arr[m - 1] + arr[m]) * 0.5f : arr[m];
    }

    private static void BoxBlur(float[] r, float[] g, float[] b, float[] a, int rw, int rh, int passes)
    {
        if (passes <= 0)
            return;
        var tr = new float[r.Length];
        var tg = new float[g.Length];
        var tb = new float[b.Length];
        var ta = new float[a.Length];

        for (int p = 0; p < passes; p++)
        {
            for (int y = 0; y < rh; y++)
            {
                for (int x = 0; x < rw; x++)
                {
                    float sr = 0, sg = 0, sb = 0, sa = 0, w = 0;
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int nx = x + dx;
                        if ((uint)nx >= (uint)rw)
                            continue;
                        int ni = y * rw + nx;
                        float ww = 3 - Math.Abs(dx);
                        sr += r[ni] * ww; sg += g[ni] * ww; sb += b[ni] * ww; sa += a[ni] * ww;
                        w += ww;
                    }
                    int i = y * rw + x;
                    tr[i] = sr / w; tg[i] = sg / w; tb[i] = sb / w; ta[i] = sa / w;
                }
            }
            Array.Copy(tr, r, r.Length);
            Array.Copy(tg, g, g.Length);
            Array.Copy(tb, b, b.Length);
            Array.Copy(ta, a, a.Length);

            for (int y = 0; y < rh; y++)
            {
                for (int x = 0; x < rw; x++)
                {
                    float sr = 0, sg = 0, sb = 0, sa = 0, w = 0;
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        int ny = y + dy;
                        if ((uint)ny >= (uint)rh)
                            continue;
                        int ni = ny * rw + x;
                        float ww = 3 - Math.Abs(dy);
                        sr += r[ni] * ww; sg += g[ni] * ww; sb += b[ni] * ww; sa += a[ni] * ww;
                        w += ww;
                    }
                    int i = y * rw + x;
                    tr[i] = sr / w; tg[i] = sg / w; tb[i] = sb / w; ta[i] = sa / w;
                }
            }
            Array.Copy(tr, r, r.Length);
            Array.Copy(tg, g, g.Length);
            Array.Copy(tb, b, b.Length);
            Array.Copy(ta, a, a.Length);
        }
    }

    private static bool IsInsideShape(float x, float y, Vec2[] pts, float lineHalf)
    {
        if (pts.Length == 1)
        {
            float dx = x - pts[0].X;
            float dy = y - pts[0].Y;
            return dx * dx + dy * dy <= lineHalf * lineHalf * 4f;
        }

        if (pts.Length == 2)
            return DistanceToSegment(x, y, pts[0], pts[1]) <= lineHalf;

        return PointInPolygon(x, y, pts);
    }

    private static float DistanceToShapeBoundary(float x, float y, Vec2[] pts, float lineHalf)
    {
        if (pts.Length == 1)
        {
            float dx = x - pts[0].X;
            float dy = y - pts[0].Y;
            return Math.Abs(MathF.Sqrt(dx * dx + dy * dy) - lineHalf * 2f);
        }

        if (pts.Length == 2)
            return Math.Abs(DistanceToSegment(x, y, pts[0], pts[1]) - lineHalf);

        return DistanceToPolygonEdges(x, y, pts);
    }

    private static float DistanceToSegment(float x, float y, Vec2 a, Vec2 b)
    {
        float vx = b.X - a.X, vy = b.Y - a.Y;
        float len2 = vx * vx + vy * vy;
        if (len2 < 1e-6f)
        {
            float dx = x - a.X, dy = y - a.Y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }
        float t = Math.Clamp(((x - a.X) * vx + (y - a.Y) * vy) / len2, 0f, 1f);
        float px = a.X + t * vx, py = a.Y + t * vy;
        float dx2 = x - px, dy2 = y - py;
        return MathF.Sqrt(dx2 * dx2 + dy2 * dy2);
    }

    private static float DistanceToPolygonEdges(float x, float y, Vec2[] pts)
    {
        float best = float.MaxValue;
        for (int i = 0; i < pts.Length; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % pts.Length];
            best = Math.Min(best, DistanceToSegment(x, y, a, b));
        }
        return best;
    }

    private static bool PointInPolygon(float x, float y, Vec2[] pts)
    {
        bool inside = false;
        for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
        {
            float xi = pts[i].X, yi = pts[i].Y;
            float xj = pts[j].X, yj = pts[j].Y;
            bool intersect = ((yi > y) != (yj > y))
                             && (x < (xj - xi) * (y - yi) / ((yj - yi) + 1e-12f) + xi);
            if (intersect)
                inside = !inside;
        }
        return inside;
    }

    private static float SoftStep(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private readonly struct Vec2(float x, float y)
    {
        public float X { get; } = x;
        public float Y { get; } = y;
    }
}
