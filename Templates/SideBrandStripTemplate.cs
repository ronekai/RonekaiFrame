using RonekaiImageFramer.Models;
using RonekaiImageFramer.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ImgSize = SixLabors.ImageSharp.Size;
using ImgRectangle = SixLabors.ImageSharp.Rectangle;

namespace RonekaiImageFramer.Templates;

public sealed class SideBrandStripTemplate : TemplateBase
{
    private static BrandColorTheme Theme => BrandThemeContext.Current;

    public override string Id => "side-brand-strip";
    public override string Name => "Yan Marka Şeridi";
    public override string Description => "Sol dikey marka şeridi + ürün sağda.";
    public override ImgSize OutputSize => new(1200, 1200);

    protected override Image<Rgba32> Render(Image<Rgba32> source)
    {
        var canvas = CreateCanvas(OutputSize);
        int stripWidth = (int)(OutputSize.Width * 0.14);
        var productBounds = new ImgRectangle(stripWidth + 40, 50, OutputSize.Width - stripWidth - 90, OutputSize.Height - 100);

        canvas.Mutate(ctx => ctx.Fill(Theme.Background));

        var strip = new ImgRectangle(0, 0, stripWidth, OutputSize.Height);
        canvas.Mutate(ctx =>
        {
            ctx.Fill(Theme.BarBackground, strip);
            ctx.Fill(Theme.AccentLine, new RectangleF(stripWidth - 3, 0, 3, OutputSize.Height));
        });

        DrawProductContained(canvas, source, productBounds, Theme.ProductSurface, fillEntireCanvas: false);

        var brandBar = new ImgRectangle(0, 0, stripWidth, 120);
        BrandRenderer.DrawBrandOnBar(canvas, brandBar);
        return canvas;
    }
}
