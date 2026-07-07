using RonekaiImageFramer.Models;

using RonekaiImageFramer.Services;

using SixLabors.ImageSharp;

using SixLabors.ImageSharp.Drawing.Processing;

using SixLabors.ImageSharp.PixelFormats;

using SixLabors.ImageSharp.Processing;

using ImgSize = SixLabors.ImageSharp.Size;

using ImgRectangle = SixLabors.ImageSharp.Rectangle;



namespace RonekaiImageFramer.Templates;



public sealed class BannerStripTemplate : TemplateBase

{

    protected override TemplateBrandPlacement BrandTextPlacement => TemplateBrandPlacement.None;



    public override string Id => "banner-strip";

    public override string Name => "Üst Alt Şerit";

    public override string Description => "İnce üst ve alt marka şeritleri.";

    public override ImgSize OutputSize => new(1200, 1200);



    protected override Image<Rgba32> Render(Image<Rgba32> source)

    {

        var canvas = CreateCanvas(OutputSize);

        int strip = (int)(OutputSize.Height * 0.07);

        LogoPlacementContext.ReserveTop(strip + 12);

        LogoPlacementContext.ReserveBottom(strip + 12);

        var bounds = new ImgRectangle(40, strip + 24, OutputSize.Width - 80, OutputSize.Height - strip * 2 - 48);



        FillCanvasBackground(canvas);

        DrawProductContained(canvas, source, bounds, ThemeColorSlot.Background, fillEntireCanvas: false);



        var top = new ImgRectangle(0, 0, OutputSize.Width, strip);

        var bottom = new ImgRectangle(0, OutputSize.Height - strip, OutputSize.Width, strip);

        canvas.Mutate(ctx =>

        {

            ThemeFillRenderer.Fill(ctx, top, ThemeColorSlot.MainText);

            ThemeFillRenderer.Fill(ctx, bottom, ThemeColorSlot.MainText);

        });



        BrandRenderer.DrawBrandOnBar(canvas, top);

        return canvas;

    }

}


