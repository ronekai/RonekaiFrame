using RonekaiImageFramer.Models;

using RonekaiImageFramer.Services;

using SixLabors.ImageSharp;

using SixLabors.ImageSharp.Drawing.Processing;

using SixLabors.ImageSharp.PixelFormats;

using SixLabors.ImageSharp.Processing;

using ImgSize = SixLabors.ImageSharp.Size;

using ImgRectangle = SixLabors.ImageSharp.Rectangle;



namespace RonekaiImageFramer.Templates;



public sealed class StoryVerticalTemplate : TemplateBase

{

    protected override TemplateBrandPlacement BrandTextPlacement => TemplateBrandPlacement.None;



    public override string Id => "story-vertical";

    public override string Name => "Hikaye Dikey";

    public override string Description => "1080×1920 story / reels tam dikey.";

    public override ImgSize OutputSize => new(1080, 1920);



    protected override Image<Rgba32> Render(Image<Rgba32> source)

    {

        var canvas = CreateCanvas(OutputSize);

        int barH = (int)(OutputSize.Height * 0.08);

        LogoPlacementContext.ReserveTop(barH + 12);

        LogoPlacementContext.ReserveBottom(barH + 16);

        var productBounds = new ImgRectangle(48, barH + 40, OutputSize.Width - 96, OutputSize.Height - barH * 2 - 80);



        FillCanvasBackground(canvas);

        DrawProductContained(canvas, source, productBounds, ThemeColorSlot.Background, fillEntireCanvas: false);



        var topBar = new ImgRectangle(0, 0, OutputSize.Width, barH);

        var bottomBar = new ImgRectangle(0, OutputSize.Height - barH, OutputSize.Width, barH);

        canvas.Mutate(ctx =>

        {

            ThemeFillRenderer.Fill(ctx, topBar, ThemeColorSlot.MainText);

            ThemeFillRenderer.Fill(ctx, bottomBar, ThemeColorSlot.MainText);

            ThemeFillRenderer.Fill(ctx, new RectangleF(0, barH - 3, OutputSize.Width, 3), ThemeColorSlot.Suffix);

        });



        BrandRenderer.DrawBrandOnBar(canvas, bottomBar);

        return canvas;

    }

}


