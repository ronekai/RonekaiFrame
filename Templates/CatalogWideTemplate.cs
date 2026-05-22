using RonekaiImageFramer.Models;
using RonekaiImageFramer.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ImgSize = SixLabors.ImageSharp.Size;
using ImgRectangle = SixLabors.ImageSharp.Rectangle;

namespace RonekaiImageFramer.Templates;

public sealed class CatalogWideTemplate : TemplateBase
{
    protected override TemplateBrandPlacement BrandTextPlacement => TemplateBrandPlacement.None;

    public override string Id => "catalog-wide";
    public override string Name => "Katalog Geniş";
    public override string Description => "1200×628 mağaza banner / kapak görseli.";
    public override ImgSize OutputSize => new(1200, 628);

    protected override Image<Rgba32> Render(Image<Rgba32> source)
    {
        var canvas = CreateCanvas(OutputSize);
        FillCanvasBackground(canvas);

        int sidePad = 48;
        int barHeight = (int)(OutputSize.Height * 0.18);
        LogoPlacementContext.ReserveBottom(barHeight + 16);
        var productBounds = new ImgRectangle(
            sidePad, sidePad / 2,
            OutputSize.Width - sidePad * 2,
            OutputSize.Height - barHeight - sidePad);

        DrawProductContained(canvas, source, productBounds, ThemeColorSlot.Background, fillEntireCanvas: false);

        var bar = new ImgRectangle(0, OutputSize.Height - barHeight, OutputSize.Width, barHeight);
        canvas.Mutate(ctx =>
        {
            ThemeFillRenderer.Fill(ctx, bar, ThemeColorSlot.MainText);
            ThemeFillRenderer.Fill(ctx, new RectangleF(0, bar.Y, OutputSize.Width, 3), ThemeColorSlot.Suffix);
        });

        BrandRenderer.DrawBrandOnBar(canvas, bar);
        return canvas;
    }
}
