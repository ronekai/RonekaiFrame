using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ImgPointF = SixLabors.ImageSharp.PointF;
using ImgRect = SixLabors.ImageSharp.Rectangle;

namespace RonekaiImageFramer.Services;

/// <summary>Canlı önizlemeden pin çokgeni kesitini PNG olarak çıkarır.</summary>
public static class ClonePatchBakeService
{
    public sealed record BakeResult(byte[] Png, int CanvasWidth, int CanvasHeight, int PatchWidth, int PatchHeight);

    public static BakeResult? Bake(
        byte[] previewPng,
        IReadOnlyList<(double X, double Y)> polygonNorm,
        int logicalCanvasWidth,
        int logicalCanvasHeight)
    {
        if (previewPng.Length == 0 || polygonNorm.Count < 3)
            return null;

        try
        {
            using var full = Image.Load<Rgba32>(previewPng);
            int w = full.Width;
            int h = full.Height;
            if (w < 2 || h < 2)
                return null;

            int canvasW = logicalCanvasWidth > 0 ? logicalCanvasWidth : w;
            int canvasH = logicalCanvasHeight > 0 ? logicalCanvasHeight : h;

            // Norm (0..1) → önizleme pikseli; mantıksal tuval boyutu saklanır (damga ölçeklemesi için)
            float PxX(double n) => (float)(Math.Clamp(n, 0, 1) * (w - 1));
            float PxY(double n) => (float)(Math.Clamp(n, 0, 1) * (h - 1));

            var poly = polygonNorm.Select(p => (PxX(p.X), PxY(p.Y))).ToArray();

            float left = poly.Min(p => p.Item1);
            float top = poly.Min(p => p.Item2);
            float right = poly.Max(p => p.Item1);
            float bottom = poly.Max(p => p.Item2);
            int x0 = Math.Clamp((int)Math.Floor(left), 0, w - 1);
            int y0 = Math.Clamp((int)Math.Floor(top), 0, h - 1);
            int x1 = Math.Clamp((int)Math.Ceiling(right) + 1, x0 + 1, w);
            int y1 = Math.Clamp((int)Math.Ceiling(bottom) + 1, y0 + 1, h);
            int rw = x1 - x0;
            int rh = y1 - y0;

            using var patch = full.Clone(ctx => ctx.Crop(new ImgRect(x0, y0, rw, rh)));

            // Çokgen maskesi: ray-cast yerine vektör dolgu (concave dahil)
            var localPts = poly
                .Select(p => new ImgPointF(p.Item1 - x0, p.Item2 - y0))
                .ToArray();
            ApplyPolygonAlphaMask(patch, localPts);

            using var outMs = new MemoryStream();
            patch.Save(outMs, new PngEncoder());
            return new BakeResult(outMs.ToArray(), canvasW, canvasH, rw, rh);
        }
        catch
        {
            return null;
        }
    }

    private static void ApplyPolygonAlphaMask(Image<Rgba32> patch, ImgPointF[] localPolygon)
    {
        if (localPolygon.Length < 3)
            return;

        using var mask = new Image<Rgba32>(patch.Width, patch.Height, Color.Transparent);
        var path = new PathBuilder();
        path.SetOrigin(new ImgPointF(0, 0));
        path.AddLines(localPolygon);
        path.CloseFigure();
        var built = path.Build();
        mask.Mutate(ctx => ctx.Fill(Color.White, built));

        patch.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < patch.Height; y++)
            {
                var srcRow = accessor.GetRowSpan(y);
                for (int x = 0; x < patch.Width; x++)
                {
                    if (mask[x, y].A == 0)
                        srcRow[x] = new Rgba32(0, 0, 0, 0);
                }
            }
        });
    }
}
