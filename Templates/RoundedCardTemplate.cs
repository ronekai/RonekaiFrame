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



public sealed class RoundedCardTemplate : TemplateBase

{

    public override string Id => "rounded-card";

    public override string Name => "Yuvarlak Köşe Kart";

    public override string Description => "Ürün yuvarlatılmış kart içinde.";

    public override ImgSize OutputSize => new(1200, 1200);



    protected override Image<Rgba32> Render(Image<Rgba32> source)

    {

        var canvas = CreateCanvas(OutputSize);

        FillCanvasBackground(canvas);



        int pad = (int)(OutputSize.Width * 0.1);

        var card = new RectangleF(pad, pad, OutputSize.Width - pad * 2, OutputSize.Height - pad * 2);

        var cardPath = new RectangularPolygon(card);



        canvas.Mutate(ctx =>

        {

            ctx.Fill(BrandThemeColors.InnerPanel, cardPath);

            ctx.Draw(BrandThemeColors.AccentLine, 2, cardPath);

        });



        var inner = new ImgRectangle((int)card.X + 40, (int)card.Y + 40, (int)card.Width - 80, (int)card.Height - 80);

        DrawProductContained(canvas, source, inner, ThemeColorSlot.Background, fillEntireCanvas: false);

        return canvas;

    }

}


