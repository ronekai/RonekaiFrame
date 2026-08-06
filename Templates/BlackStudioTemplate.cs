using RonekaiImageFramer.Models;
using RonekaiImageFramer.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ImgSize = SixLabors.ImageSharp.Size;
using ImgRectangle = SixLabors.ImageSharp.Rectangle;

namespace RonekaiImageFramer.Templates;

/// <summary>Beyaz Stüdyo ile aynı ölçü (1200×1200); zemin renk paletinden gelir (varsayılan koyu önerilir).</summary>
public sealed class BlackStudioTemplate : TemplateBase
{
    public override string Id => "black-studio";
    public override string Name => "Siyah Stüdyo";
    public override string Description => "1200×1200 stüdyo. Zemin renk paletinden (Damla/Seç ile değiştirilebilir); ürün ortada.";
    public override ImgSize OutputSize => new(1200, 1200);

    protected override Image<Rgba32> Render(Image<Rgba32> source)
    {
        var canvas = CreateCanvas(OutputSize);
        var padding = (int)(OutputSize.Width * 0.08);
        var bounds = new ImgRectangle(padding, padding, OutputSize.Width - padding * 2, OutputSize.Height - padding * 2);
        DrawProductContained(canvas, source, bounds, ThemeColorSlot.Background);
        return canvas;
    }
}
