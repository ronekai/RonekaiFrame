using RonekaiImageFramer.Models;
using RonekaiImageFramer.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ImgSize = SixLabors.ImageSharp.Size;
using ImgRectangle = SixLabors.ImageSharp.Rectangle;
using ImgColor = SixLabors.ImageSharp.Color;

namespace RonekaiImageFramer.Templates;

/// <summary>Beyaz Stüdyo ile aynı ölçü (1200×1200), siyah zemin.</summary>
public sealed class BlackStudioTemplate : TemplateBase
{
    public override string Id => "black-studio";
    public override string Name => "Siyah Stüdyo";
    public override string Description => "Beyaz Stüdyo ile aynı ölçü (1200×1200), siyah zemin, ürün ortada.";
    public override ImgSize OutputSize => new(1200, 1200);

    protected override Image<Rgba32> Render(Image<Rgba32> source)
    {
        var canvas = CreateCanvas(OutputSize);
        var padding = (int)(OutputSize.Width * 0.08);
        var bounds = new ImgRectangle(padding, padding, OutputSize.Width - padding * 2, OutputSize.Height - padding * 2);
        DrawProductContained(canvas, source, bounds, ImgColor.Black);
        return canvas;
    }
}
