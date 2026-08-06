using RonekaiImageFramer.Models;
using RonekaiImageFramer.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ImgSize = SixLabors.ImageSharp.Size;
using ImgRectangle = SixLabors.ImageSharp.Rectangle;

namespace RonekaiImageFramer.Templates;

public sealed class InstagramPostPortraitWhiteTemplate : TemplateBase
{
    protected override TemplateBrandPlacement BrandTextPlacement => TemplateBrandPlacement.None;

    public override string Id => "instagram-post-1080x1350-white";

    public override string Name => "Instagram Post 1080×1350 (Beyaz)";

    public override string Description => "1080×1350 dikey post. Görsel bozulmadan oturur; boşluklar renk paleti zemininden gelir.";

    public override ImgSize OutputSize => new(1080, 1350);

    protected override Image<Rgba32> Render(Image<Rgba32> source)
    {
        var canvas = CreateCanvas(OutputSize);
        int pad = 60;
        var bounds = new ImgRectangle(pad, pad, OutputSize.Width - pad * 2, OutputSize.Height - pad * 2);
        DrawProductContained(canvas, source, bounds, ThemeColorSlot.Background, fillEntireCanvas: true);
        return canvas;
    }
}
