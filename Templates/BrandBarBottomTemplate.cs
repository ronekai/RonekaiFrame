using RonekaiImageFramer.Models;
using RonekaiImageFramer.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ImgSize = SixLabors.ImageSharp.Size;
using ImgRectangle = SixLabors.ImageSharp.Rectangle;

namespace RonekaiImageFramer.Templates;

public sealed class BrandBarBottomTemplate : TemplateBase
{
    private static BrandColorTheme Theme => BrandThemeContext.Current;

    public override string Id => "brand-bar-bottom";
    public override string Name => "Marka Şeridi (Alt)";
    public override string Description => "Ürün alanı + alt RONEKAI.DEN şeridi.";
    public override ImgSize OutputSize => new(1200, 1500);

    protected override Image<Rgba32> Render(Image<Rgba32> source)
    {
        var canvas = CreateCanvas(OutputSize);
        int barHeight = (int)(OutputSize.Height * 0.11);
        var productBounds = new ImgRectangle(60, 60, OutputSize.Width - 120, OutputSize.Height - barHeight - 100);

        DrawProductContained(canvas, source, productBounds, Theme.ProductSurface);

        var bar = new ImgRectangle(0, OutputSize.Height - barHeight, OutputSize.Width, barHeight);
        canvas.Mutate(ctx =>
        {
            ctx.Fill(Theme.BarBackground, bar);
            ctx.Fill(Theme.AccentLine, new RectangleF(0, bar.Y, OutputSize.Width, 4));
        });

        BrandRenderer.DrawBrandOnBar(canvas, bar);
        return canvas;
    }
}
