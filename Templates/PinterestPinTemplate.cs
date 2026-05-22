using RonekaiImageFramer.Models;
using RonekaiImageFramer.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ImgSize = SixLabors.ImageSharp.Size;
using ImgRectangle = SixLabors.ImageSharp.Rectangle;

namespace RonekaiImageFramer.Templates;

public sealed class PinterestPinTemplate : TemplateBase
{
    public override string Id => "pinterest-pin";
    public override string Name => "Pinterest Pin";
    public override string Description => "1000×1500 dikey pin formatı.";
    public override ImgSize OutputSize => new(1000, 1500);

    protected override Image<Rgba32> Render(Image<Rgba32> source)
    {
        var canvas = CreateCanvas(OutputSize);
        int pad = (int)(OutputSize.Width * 0.07);
        var bounds = new ImgRectangle(pad, pad, OutputSize.Width - pad * 2, OutputSize.Height - pad * 2);
        DrawProductContained(canvas, source, bounds, ThemeColorSlot.Background);
        return canvas;
    }
}
