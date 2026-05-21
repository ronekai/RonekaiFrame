using RonekaiImageFramer.Models;
using RonekaiImageFramer.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ImgSize = SixLabors.ImageSharp.Size;
using ImgRectangle = SixLabors.ImageSharp.Rectangle;

namespace RonekaiImageFramer.Templates;

public sealed class DoubleLineFrameTemplate : TemplateBase
{
    private static BrandColorTheme Theme => BrandThemeContext.Current;

    public override string Id => "double-line";
    public override string Name => "Çift Çizgi Çerçeve";
    public override string Description => "İnce çift çerçeve, ürün ortada.";
    public override ImgSize OutputSize => new(1200, 1200);

    protected override Image<Rgba32> Render(Image<Rgba32> source)
    {
        var canvas = CreateCanvas(OutputSize);
        int outer = 24;
        int inner = 36;
        var bounds = new ImgRectangle(inner, inner, OutputSize.Width - inner * 2, OutputSize.Height - inner * 2);

        DrawProductContained(canvas, source, bounds, Theme.Background);

        canvas.Mutate(ctx =>
        {
            ctx.Draw(Theme.FrameBorder, 2, new RectangleF(outer, outer, OutputSize.Width - outer * 2, OutputSize.Height - outer * 2));
            ctx.Draw(Theme.AccentLine, 1, new RectangleF(inner - 4, inner - 4, OutputSize.Width - (inner - 4) * 2, OutputSize.Height - (inner - 4) * 2));
        });

        return canvas;
    }
}
