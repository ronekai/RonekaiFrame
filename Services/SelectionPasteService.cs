using RonekaiImageFramer.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace RonekaiImageFramer.Services;

/// <summary>
/// Seçim alanından yama çıkarır; döndürerek hedefe yapıştırır.
/// </summary>
public static class SelectionPasteService
{
    public static void ApplyAll(Image<Rgba32> image, IReadOnlyList<SelectionPasteOp> ops)
    {
        if (ops is null || ops.Count == 0)
            return;
        foreach (var op in ops)
            Apply(image, op);
    }

    public static void Apply(Image<Rgba32> image, SelectionPasteOp op)
    {
        if (op.PatchPng is null || op.PatchPng.Length == 0)
            return;
        if (image.Width < 4 || image.Height < 4)
            return;

        using var patch = Image.Load<Rgba32>(op.PatchPng);
        if (patch.Width < 1 || patch.Height < 1)
            return;

        double angle = op.RotationDegrees % 360.0;
        using var rotated = patch.CloneAs<Rgba32>();
        if (Math.Abs(angle) > 0.05)
        {
            rotated.Mutate(ctx => ctx.Rotate((float)angle));
        }

        int w = image.Width;
        int h = image.Height;
        float cx = (float)(Math.Clamp(op.DestCenter.X, 0, 1) * (w - 1));
        float cy = (float)(Math.Clamp(op.DestCenter.Y, 0, 1) * (h - 1));
        int destX = (int)Math.Round(cx - rotated.Width / 2.0);
        int destY = (int)Math.Round(cy - rotated.Height / 2.0);

        image.Mutate(ctx =>
        {
            ctx.DrawImage(rotated, new Point(destX, destY), 1f);
        });
    }

    /// <summary>
    /// Kaynak görselden seçim dikdörtgenini (şekil maskeli) PNG yama olarak çıkarır.
    /// </summary>
    public static bool TryExtractPatch(
        Image<Rgba32> source,
        NormalizedCropRect sourceRect,
        TextureCloneBrushShape shape,
        out byte[] pngBytes,
        out int patchW,
        out int patchH)
    {
        pngBytes = [];
        patchW = 0;
        patchH = 0;
        if (source.Width < 4 || source.Height < 4)
            return false;

        double left = Math.Clamp(sourceRect.Left, 0, 1);
        double top = Math.Clamp(sourceRect.Top, 0, 1);
        double right = Math.Clamp(sourceRect.Left + sourceRect.Width, 0, 1);
        double bottom = Math.Clamp(sourceRect.Top + sourceRect.Height, 0, 1);
        if (right - left < 0.001 || bottom - top < 0.001)
            return false;

        int x0 = Math.Clamp((int)Math.Floor(left * source.Width), 0, source.Width - 1);
        int y0 = Math.Clamp((int)Math.Floor(top * source.Height), 0, source.Height - 1);
        int x1 = Math.Clamp((int)Math.Ceiling(right * source.Width), x0 + 1, source.Width);
        int y1 = Math.Clamp((int)Math.Ceiling(bottom * source.Height), y0 + 1, source.Height);
        int rw = x1 - x0;
        int rh = y1 - y0;
        if (rw < 1 || rh < 1)
            return false;

        using var patch = new Image<Rgba32>(rw, rh);
        float cx = (rw - 1) / 2f;
        float cy = (rh - 1) / 2f;
        float rx = Math.Max(1f, rw / 2f);
        float ry = Math.Max(1f, rh / 2f);

        for (int y = 0; y < rh; y++)
        {
            for (int x = 0; x < rw; x++)
            {
                var p = source[x0 + x, y0 + y];
                float mask = ShapeMask(x, y, cx, cy, rx, ry, shape);
                if (mask < 0.01f)
                {
                    patch[x, y] = new Rgba32(0, 0, 0, 0);
                    continue;
                }

                byte a = (byte)Math.Clamp(p.A * mask + 0.5f, 0, 255);
                patch[x, y] = new Rgba32(p.R, p.G, p.B, a);
            }
        }

        using var ms = new MemoryStream();
        patch.Save(ms, new PngEncoder());
        pngBytes = ms.ToArray();
        patchW = rw;
        patchH = rh;
        return pngBytes.Length > 0;
    }

    private static float ShapeMask(
        int x, int y, float cx, float cy, float rx, float ry, TextureCloneBrushShape shape)
    {
        float dx = (x - cx) / rx;
        float dy = (y - cy) / ry;
        float d = shape switch
        {
            TextureCloneBrushShape.Circle => MathF.Sqrt(dx * dx + dy * dy),
            TextureCloneBrushShape.Ellipse => MathF.Sqrt(dx * dx + dy * dy),
            TextureCloneBrushShape.SoftSquare =>
                MathF.Pow(MathF.Pow(MathF.Abs(dx), 4f) + MathF.Pow(MathF.Abs(dy), 4f), 0.25f),
            _ => Math.Max(MathF.Abs(dx), MathF.Abs(dy)) // Square / Normal
        };

        if (d <= 0.82f)
            return 1f;
        if (d >= 1f)
            return 0f;
        float t = 1f - (d - 0.82f) / 0.18f;
        return t * t * (3f - 2f * t);
    }
}
