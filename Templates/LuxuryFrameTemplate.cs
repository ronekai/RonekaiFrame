using RonekaiImageFramer.Models;

using RonekaiImageFramer.Services;

using SixLabors.ImageSharp;

using SixLabors.ImageSharp.Drawing.Processing;

using SixLabors.ImageSharp.PixelFormats;

using SixLabors.ImageSharp.Processing;

using ImgSize = SixLabors.ImageSharp.Size;

using ImgRectangle = SixLabors.ImageSharp.Rectangle;



namespace RonekaiImageFramer.Templates;



public sealed class LuxuryFrameTemplate : TemplateBase

{

    public override string Id => "luxury-frame";

    public override string Name => "Lüks Çift Çerçeve";

    public override string Description => "İnce altın tonlu çift çerçeve, koyu zemin.";

    public override ImgSize OutputSize => new(1200, 1500);



    protected override Image<Rgba32> Render(Image<Rgba32> source)

    {

        var canvas = CreateCanvas(OutputSize);

        int outer = 28;

        int inner = 44;

        FillCanvasBackground(canvas);



        var bounds = new ImgRectangle(inner, inner, OutputSize.Width - inner * 2, OutputSize.Height - inner * 2);

        DrawProductContained(canvas, source, bounds, BrandThemeColors.InnerPanel, fillEntireCanvas: false);



        canvas.Mutate(ctx =>

        {

            ctx.Draw(BrandThemeColors.AccentLine, 2, new RectangleF(outer, outer, OutputSize.Width - outer * 2, OutputSize.Height - outer * 2));

            ctx.Draw(BrandThemeColors.FrameBorder, 1, new RectangleF(inner - 6, inner - 6, OutputSize.Width - (inner - 6) * 2, OutputSize.Height - (inner - 6) * 2));

        });



        return canvas;

    }

}


