using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ImgColor = SixLabors.ImageSharp.Color;
using ImgSize = SixLabors.ImageSharp.Size;
using ImgPoint = SixLabors.ImageSharp.Point;
using ImgRectangle = SixLabors.ImageSharp.Rectangle;

namespace RonekaiImageFramer.Templates;

public abstract class TemplateBase : IProductTemplate
{
    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract ImgSize OutputSize { get; }

    public Image<Rgba32> Apply(Image<Rgba32> source)
    {
        using var clone = source.CloneAs<Rgba32>();
        return Render(clone);
    }

    protected abstract Image<Rgba32> Render(Image<Rgba32> source);

    protected static Image<Rgba32> CreateCanvas(ImgSize size) =>
        new(size.Width, size.Height);

    protected static void DrawProductContained(
        Image<Rgba32> canvas,
        Image<Rgba32> product,
        ImgRectangle targetBounds,
        ImgColor background,
        bool fillEntireCanvas = true)
    {
        if (fillEntireCanvas)
            canvas.Mutate(ctx => ctx.Fill(background, new RectangleF(0, 0, canvas.Width, canvas.Height)));
        else
            canvas.Mutate(ctx => ctx.Fill(background, targetBounds));

        var fit = CalculateFit(product.Size, targetBounds.Size);
        using var resized = product.Clone(ctx => ctx.Resize(new ResizeOptions
        {
            Size = fit,
            Mode = ResizeMode.Max,
            Sampler = KnownResamplers.Lanczos3
        }));

        int posX = targetBounds.X + (targetBounds.Width - resized.Width) / 2;
        int posY = targetBounds.Y + (targetBounds.Height - resized.Height) / 2;

        canvas.Mutate(ctx => ctx.DrawImage(resized, new ImgPoint(posX, posY), 1f));
    }

    protected static ImgSize CalculateFit(ImgSize source, ImgSize bounds)
    {
        float scale = Math.Min(bounds.Width / (float)source.Width, bounds.Height / (float)source.Height);
        int w = Math.Max(1, (int)(source.Width * scale));
        int h = Math.Max(1, (int)(source.Height * scale));
        return new ImgSize(w, h);
    }
}
