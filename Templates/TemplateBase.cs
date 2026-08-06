using RonekaiImageFramer.Models;
using RonekaiImageFramer.Services;
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

    public virtual bool IsPassthrough => false;

    public virtual bool StretchToExport => false;

    /// <summary>Varsayılan: stüdyo/kare şablonlarda köşe marka metni.</summary>
    protected virtual TemplateBrandPlacement BrandTextPlacement => TemplateBrandPlacement.Corner;

    public Image<Rgba32> Apply(Image<Rgba32> source)
    {
        using var clone = source.CloneAs<Rgba32>();
        var canvas = Render(clone);
        if (!BrandOverlayDeferContext.IsDeferred)
        {
            if (BrandTextPlacement == TemplateBrandPlacement.Corner)
                DrawCornerBrand(canvas);
            ImageBrandOverlay.ApplyBrandLogo(canvas);
        }
        return canvas;
    }

    protected abstract Image<Rgba32> Render(Image<Rgba32> source);

    protected static void DrawCornerBrand(Image<Rgba32> canvas, int margin = 24)
    {
        LogoPlacementContext.ReserveCornerBrand(canvas.Width, margin);
        BrandRenderer.DrawCornerWatermark(canvas, margin);
    }

    protected static Image<Rgba32> CreateCanvas(ImgSize size) =>
        new(size.Width, size.Height);

    protected static void FillCanvasBackground(Image<Rgba32> canvas) =>
        ThemeFillRenderer.Fill(canvas, new RectangleF(0, 0, canvas.Width, canvas.Height), ThemeColorSlot.Background);

    protected static void FillRegion(Image<Rgba32> canvas, RectangleF rect, ThemeColorSlot slot) =>
        ThemeFillRenderer.Fill(canvas, rect, slot);

    protected static void DrawProductContained(
        Image<Rgba32> canvas,
        Image<Rgba32> product,
        ImgRectangle targetBounds,
        ThemeColorSlot backgroundSlot,
        bool fillEntireCanvas = true)
    {
        if (fillEntireCanvas)
            ThemeFillRenderer.Fill(canvas, new RectangleF(0, 0, canvas.Width, canvas.Height), backgroundSlot);
        else
            ThemeFillRenderer.Fill(canvas, targetBounds, backgroundSlot);

        DrawProductIntoBounds(canvas, product, targetBounds);
    }

    protected static void DrawProductContained(
        Image<Rgba32> canvas,
        Image<Rgba32> product,
        ImgRectangle targetBounds,
        ImgColor solidBackground,
        bool fillEntireCanvas = true)
    {
        if (fillEntireCanvas)
            canvas.Mutate(ctx => ctx.Fill(solidBackground, new RectangleF(0, 0, canvas.Width, canvas.Height)));
        else
            canvas.Mutate(ctx => ctx.Fill(solidBackground, targetBounds));

        DrawProductIntoBounds(canvas, product, targetBounds);
    }

    private static void DrawProductIntoBounds(
        Image<Rgba32> canvas,
        Image<Rgba32> product,
        ImgRectangle targetBounds)
    {
        if (ProcessingFitContext.ResponsiveProductFit)
        {
            using var resized = product.Clone(ctx => ctx.Resize(new ResizeOptions
            {
                Size = targetBounds.Size,
                Mode = ResizeMode.Crop,
                Position = AnchorPositionMode.Center,
                Sampler = KnownResamplers.Lanczos3
            }));
            canvas.Mutate(ctx => ctx.DrawImage(resized, new ImgPoint(targetBounds.X, targetBounds.Y), 1f));
            ProductPlacementContext.Set(
                product.Width, product.Height,
                canvas.Width, canvas.Height,
                targetBounds.X, targetBounds.Y, targetBounds.Width, targetBounds.Height);
            return;
        }

        var fit = CalculateFit(product.Size, targetBounds.Size);
        using var contained = product.Clone(ctx => ctx.Resize(new ResizeOptions
        {
            Size = fit,
            Mode = ResizeMode.Max,
            Sampler = KnownResamplers.Lanczos3
        }));

        int posX = targetBounds.X + (targetBounds.Width - contained.Width) / 2;
        int posY = targetBounds.Y + (targetBounds.Height - contained.Height) / 2;
        canvas.Mutate(ctx => ctx.DrawImage(contained, new ImgPoint(posX, posY), 1f));
        ProductPlacementContext.Set(
            product.Width, product.Height,
            canvas.Width, canvas.Height,
            posX, posY, contained.Width, contained.Height);
    }

    protected static ImgSize CalculateFit(ImgSize source, ImgSize bounds)
    {
        float scale = Math.Min(bounds.Width / (float)source.Width, bounds.Height / (float)source.Height);
        int w = Math.Max(1, (int)(source.Width * scale));
        int h = Math.Max(1, (int)(source.Height * scale));
        return new ImgSize(w, h);
    }
}
