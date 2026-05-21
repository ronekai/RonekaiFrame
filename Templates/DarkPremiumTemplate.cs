using RonekaiImageFramer.Models;
using RonekaiImageFramer.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ImgSize = SixLabors.ImageSharp.Size;
using ImgRectangle = SixLabors.ImageSharp.Rectangle;

namespace RonekaiImageFramer.Templates;

public sealed class DarkPremiumTemplate : TemplateBase
{
    private static BrandColorTheme Theme => BrandThemeContext.Current;

    public override string Id => "dark-premium";
    public override string Name => "Koyu Premium";
    public override string Description => "Çerçeveli koyu görünüm, üst marka şeridi.";
    public override ImgSize OutputSize => new(1200, 1500);

    protected override Image<Rgba32> Render(Image<Rgba32> source)
    {
        var canvas = CreateCanvas(OutputSize);
        int frame = 48;
        int header = 90;

        canvas.Mutate(ctx =>
        {
            ctx.Fill(Theme.Background);
            var inner = new ImgRectangle(frame, header + frame / 2, OutputSize.Width - frame * 2, OutputSize.Height - header - frame * 2 - frame);
            ctx.Fill(Theme.InnerPanel, inner);
        });

        var productBounds = new ImgRectangle(frame + 24, header + frame, OutputSize.Width - (frame + 24) * 2, OutputSize.Height - header - frame * 3 - 24);
        DrawProductContained(canvas, source, productBounds, Theme.InnerPanel, fillEntireCanvas: false);

        canvas.Mutate(ctx =>
        {
            ctx.Draw(Theme.AccentLine, 3, new RectangleF(frame, frame, OutputSize.Width - frame * 2, OutputSize.Height - frame * 2));
        });

        BrandRenderer.DrawBrandOnBar(canvas, new ImgRectangle(0, 0, OutputSize.Width, header));
        return canvas;
    }
}
