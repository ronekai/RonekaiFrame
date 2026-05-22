using RonekaiImageFramer.Models;
using RonekaiImageFramer.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ImgSize = SixLabors.ImageSharp.Size;
using ImgRectangle = SixLabors.ImageSharp.Rectangle;

namespace RonekaiImageFramer.Templates;

public sealed class MarketplaceSquareTemplate : TemplateBase
{
    public override string Id => "marketplace-square";
    public override string Name => "Pazar Yeri Kare";
    public override string Description => "1500×1500 kare, seçilen zemin rengi.";
    public override ImgSize OutputSize => new(1500, 1500);

    protected override Image<Rgba32> Render(Image<Rgba32> source)
    {
        var canvas = CreateCanvas(OutputSize);
        int margin = (int)(OutputSize.Width * 0.05);
        var bounds = new ImgRectangle(margin, margin, OutputSize.Width - margin * 2, OutputSize.Height - margin * 2);
        DrawProductContained(canvas, source, bounds, ThemeColorSlot.Background);
        return canvas;
    }
}
