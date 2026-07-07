using RonekaiImageFramer.Models;

using RonekaiImageFramer.Services;

using SixLabors.ImageSharp;

using SixLabors.ImageSharp.Drawing.Processing;

using SixLabors.ImageSharp.PixelFormats;

using SixLabors.ImageSharp.Processing;

using ImgSize = SixLabors.ImageSharp.Size;

using ImgRectangle = SixLabors.ImageSharp.Rectangle;



namespace RonekaiImageFramer.Templates;



public sealed class MinimalFrameTemplate : TemplateBase

{

    protected override TemplateBrandPlacement BrandTextPlacement => TemplateBrandPlacement.None;



    public override string Id => "minimal-frame";

    public override string Name => "Minimal Çerçeve";

    public override string Description => "Zemin + ince çerçeve + üst marka şeridi.";

    public override ImgSize OutputSize => new(1200, 1200);



    protected override Image<Rgba32> Render(Image<Rgba32> source)

    {

        var canvas = CreateCanvas(OutputSize);

        int border = 6;

        int header = 72;

        int pad = 56;



        FillCanvasBackground(canvas);



        var productBounds = new ImgRectangle(pad, header + pad / 2, OutputSize.Width - pad * 2, OutputSize.Height - header - pad);

        DrawProductContained(canvas, source, productBounds, ThemeColorSlot.Background, fillEntireCanvas: false);



        canvas.Mutate(ctx =>

        {

            ctx.Draw(BrandThemeColors.FrameBorder, border, new RectangleF(border, header, OutputSize.Width - border * 2, OutputSize.Height - header - border));

            ThemeFillRenderer.Fill(ctx, new ImgRectangle(0, 0, OutputSize.Width, header), ThemeColorSlot.MainText);

            ThemeFillRenderer.Fill(ctx, new ImgRectangle(0, header - 3, OutputSize.Width, 3), ThemeColorSlot.Suffix);

        });



        BrandRenderer.DrawBrandOnBar(canvas, new ImgRectangle(0, 0, OutputSize.Width, header));

        return canvas;

    }

}


