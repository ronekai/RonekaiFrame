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
    private static BrandColorTheme Theme => BrandThemeContext.Current;

    public override string Id => "brand-bar-top";
    public override string Name => "Marka Şeridi (Üst)";
    public override string Description => "Üstte marka şeridi, ürün altta.";
    public override ImgSize OutputSize => new(1200, 1500);

    protected override Image<Rgba32> Render(Image<Rgba32> source)
    {
        var canvas = CreateCanvas(OutputSize);
        int barHeight = (int)(OutputSize.Height * 0.11);
        var productBounds = new ImgRectangle(60, barHeight + 60, OutputSize.Width - 120, OutputSize.Height - barHeight - 120);

        DrawProductContained(canvas, source, productBounds, Theme.ProductSurface);

        var bar = new ImgRectangle(0, 0, OutputSize.Width, barHeight);
        canvas.Mutate(ctx =>
        {
            ctx.Fill(Theme.BarBackground, bar);
            ctx.Fill(Theme.AccentLine, new RectangleF(0, barHeight - 4, OutputSize.Width, 4));
        });

        BrandRenderer.DrawBrandOnBar(canvas, bar);
        return canvas;
    }
}
