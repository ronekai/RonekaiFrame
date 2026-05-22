using RonekaiImageFramer.Models;
using RonekaiImageFramer.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ImgSize = SixLabors.ImageSharp.Size;
using ImgRectangle = SixLabors.ImageSharp.Rectangle;

namespace RonekaiImageFramer.Templates;

public sealed class TrendyolSquareTemplate : TemplateBase
{
    public override string Id => "trendyol-square";
    public override string Name => "Pazaryeri Kare (1500)";
    public override string Description => "1500×1500 e-ticaret vitrin standardı.";
    public override ImgSize OutputSize => new(1500, 1500);

    protected override Image<Rgba32> Render(Image<Rgba32> source)
    {
        var canvas = CreateCanvas(OutputSize);
        int pad = (int)(OutputSize.Width * 0.06);
        var bounds = new ImgRectangle(pad, pad, OutputSize.Width - pad * 2, OutputSize.Height - pad * 2);
        DrawProductContained(canvas, source, bounds, ThemeColorSlot.Background);
        return canvas;
    }
}
