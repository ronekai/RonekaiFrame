using RonekaiImageFramer.Models;
using RonekaiImageFramer.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ImgSize = SixLabors.ImageSharp.Size;
using ImgRectangle = SixLabors.ImageSharp.Rectangle;

namespace RonekaiImageFramer.Templates;

public sealed class BrandBarTopTemplate : TemplateBase
{
    protected override TemplateBrandPlacement BrandTextPlacement => TemplateBrandPlacement.None;

    public override string Id => "brand-bar-top";
    public override string Name => "Marka Şeridi (Üst)";
    public override string Description => "Üstte marka şeridi, ürün altta.";
    public override ImgSize OutputSize => new(1200, 1500);

    protected override Image<Rgba32> Render(Image<Rgba32> source)
    {
        var canvas = CreateCanvas(OutputSize);
        FillCanvasBackground(canvas);

        int barHeight = (int)(OutputSize.Height * 0.11);
        LogoPlacementContext.ReserveTop(barHeight + 16);
        var productBounds = new ImgRectangle(60, barHeight + 60, OutputSize.Width - 120, OutputSize.Height - barHeight - 120);

        DrawProductContained(canvas, source, productBounds, ThemeColorSlot.Background, fillEntireCanvas: false);

        var bar = new ImgRectangle(0, 0, OutputSize.Width, barHeight);
        canvas.Mutate(ctx =>
        {
            ThemeFillRenderer.Fill(ctx, bar, ThemeColorSlot.MainText);
            ThemeFillRenderer.Fill(ctx, new RectangleF(0, barHeight - 4, OutputSize.Width, 4), ThemeColorSlot.Suffix);
        });

        BrandRenderer.DrawBrandOnBar(canvas, bar);
        return canvas;
    }
}
