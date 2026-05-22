using RonekaiImageFramer.Models;
using RonekaiImageFramer.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ImgSize = SixLabors.ImageSharp.Size;
using ImgRectangle = SixLabors.ImageSharp.Rectangle;

namespace RonekaiImageFramer.Templates;

public sealed class DiagonalAccentTemplate : TemplateBase
{
    protected override TemplateBrandPlacement BrandTextPlacement => TemplateBrandPlacement.None;

    public override string Id => "diagonal-accent";
    public override string Name => "Çapraz Vurgu";
    public override string Description => "Sol üst çapraz marka şeridi.";
    public override ImgSize OutputSize => new(1200, 1200);

    protected override Image<Rgba32> Render(Image<Rgba32> source)
    {
        var canvas = CreateCanvas(OutputSize);
        FillCanvasBackground(canvas);

        float w = OutputSize.Width * 0.42f;
        float h = OutputSize.Height * 0.28f;
        LogoPlacementContext.ReserveLeft((int)w + 16);
        LogoPlacementContext.ReserveTop((int)h + 16);
        var triangle = new PathBuilder()
            .AddLine(0, 0, w, 0)
            .AddLine(w, 0, 0, h)
            .AddLine(0, h, 0, 0)
            .CloseFigure()
            .Build();
        canvas.Mutate(ctx => ThemeFillRenderer.Fill(ctx, triangle, ThemeColorSlot.MainText));

        var bounds = new ImgRectangle(80, 60, OutputSize.Width - 120, OutputSize.Height - 120);
        DrawProductContained(canvas, source, bounds, ThemeColorSlot.Background, fillEntireCanvas: false);

        BrandRenderer.DrawBrandHorizontal(canvas, 36, 28, 22, BrandThemeColors.RonekaiOnBackground, BrandThemeColors.DenOnBackground);
        return canvas;
    }
}
