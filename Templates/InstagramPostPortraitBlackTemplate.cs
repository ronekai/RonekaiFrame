using RonekaiImageFramer.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ImgColor = SixLabors.ImageSharp.Color;
using ImgSize = SixLabors.ImageSharp.Size;
using ImgRectangle = SixLabors.ImageSharp.Rectangle;

namespace RonekaiImageFramer.Templates;

public sealed class InstagramPostPortraitBlackTemplate : TemplateBase
{
    protected override TemplateBrandPlacement BrandTextPlacement => TemplateBrandPlacement.None;

    public override string Id => "instagram-post-1080x1350-black";

    public override string Name => "Instagram Post 1080×1350 (Siyah)";

    public override string Description => "1080×1350 dikey post. Görsel bozulmadan oturur, boşluklar siyah ile tamamlanır.";

    public override ImgSize OutputSize => new(1080, 1350);

    protected override Image<Rgba32> Render(Image<Rgba32> source)
    {
        var canvas = CreateCanvas(OutputSize);
        canvas.Mutate(ctx => ctx.Fill(ImgColor.Black));

        int pad = 60;
        var bounds = new ImgRectangle(pad, pad, OutputSize.Width - pad * 2, OutputSize.Height - pad * 2);
        DrawProductContained(canvas, source, bounds, ImgColor.Black, fillEntireCanvas: false);

        return canvas;
    }
}

