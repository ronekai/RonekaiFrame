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

public sealed class PolaroidFrameTemplate : TemplateBase
{
    protected override TemplateBrandPlacement BrandTextPlacement => TemplateBrandPlacement.None;

    public override string Id => "polaroid-frame";
    public override string Name => "Polaroid Çerçeve";
    public override string Description => "Beyaz çerçeve, altta marka alanı.";
    public override ImgSize OutputSize => new(1200, 1500);

    protected override Image<Rgba32> Render(Image<Rgba32> source)
    {
        var canvas = CreateCanvas(OutputSize);
        FillCanvasBackground(canvas);

        int outer = 70;
        var frame = new ImgRectangle(outer, outer, OutputSize.Width - outer * 2, OutputSize.Height - outer * 2);
        canvas.Mutate(ctx =>
        {
            ctx.Fill(ImgColor.White, frame);
            ctx.Draw(BrandThemeColors.FrameBorder, 2, new RectangleF(frame.X, frame.Y, frame.Width, frame.Height));
        });

        int captionH = (int)(frame.Height * 0.22);
        var photoBounds = new ImgRectangle(frame.X + 36, frame.Y + 36, frame.Width - 72, frame.Height - captionH - 48);
        DrawProductContained(canvas, source, photoBounds, ImgColor.White, fillEntireCanvas: false);

        var caption = new ImgRectangle(frame.X, frame.Bottom - captionH, frame.Width, captionH);
        LogoPlacementContext.ReserveBottom(OutputSize.Height - caption.Y + 12);
        canvas.Mutate(ctx => ThemeFillRenderer.Fill(ctx, caption, ThemeColorSlot.MainText));
        BrandRenderer.DrawBrandOnBar(canvas, caption);
        return canvas;
    }
}
