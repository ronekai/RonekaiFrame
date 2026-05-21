using RonekaiImageFramer.Models;
using RonekaiImageFramer.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ImgColor = SixLabors.ImageSharp.Color;
using ImgSize = SixLabors.ImageSharp.Size;
using ImgRectangle = SixLabors.ImageSharp.Rectangle;

namespace RonekaiImageFramer.Templates;

public sealed class SoftShadowStudioTemplate : TemplateBase
{
    private static BrandColorTheme Theme => BrandThemeContext.Current;

    public override string Id => "soft-shadow";
    public override string Name => "Yumuşak Gölge Stüdyo";
    public override string Description => "Açık zemin, ürün etrafında yumuşak gölge kartı.";
    public override ImgSize OutputSize => new(1200, 1200);

    protected override Image<Rgba32> Render(Image<Rgba32> source)
    {
        var canvas = CreateCanvas(OutputSize);
        canvas.Mutate(ctx => ctx.Fill(Theme.Background));

        int pad = (int)(OutputSize.Width * 0.1);
        var card = new ImgRectangle(pad, pad, OutputSize.Width - pad * 2, OutputSize.Height - pad * 2);
        canvas.Mutate(ctx =>
        {
            ctx.Fill(ImgColor.FromRgba(0, 0, 0, 25), new RectangleF(card.X + 8, card.Y + 10, card.Width, card.Height));
            ctx.Fill(Theme.ProductSurface, card);
        });

        var inner = new ImgRectangle(card.X + 40, card.Y + 40, card.Width - 80, card.Height - 80);
        DrawProductContained(canvas, source, inner, Theme.ProductSurface, fillEntireCanvas: false);
        return canvas;
    }
}
