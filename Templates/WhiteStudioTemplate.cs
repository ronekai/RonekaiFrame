using RonekaiImageFramer.Models;
using RonekaiImageFramer.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ImgSize = SixLabors.ImageSharp.Size;
using ImgRectangle = SixLabors.ImageSharp.Rectangle;

namespace RonekaiImageFramer.Templates;

public sealed class WhiteStudioTemplate : TemplateBase
{
    private static BrandColorTheme Theme => BrandThemeContext.Current;

    public override string Id => "white-studio";
    public override string Name => "Beyaz Stüdyo";
    public override string Description => "Seçilen zemin rengi, ürün ortada.";
    public override ImgSize OutputSize => new(1200, 1200);

    protected override Image<Rgba32> Render(Image<Rgba32> source)
    {
        var canvas = CreateCanvas(OutputSize);
        var padding = (int)(OutputSize.Width * 0.08);
        var bounds = new ImgRectangle(padding, padding, OutputSize.Width - padding * 2, OutputSize.Height - padding * 2);
        DrawProductContained(canvas, source, bounds, Theme.Background);
        return canvas;
    }
}
