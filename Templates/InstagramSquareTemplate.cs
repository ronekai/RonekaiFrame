using RonekaiImageFramer.Models;
using RonekaiImageFramer.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ImgSize = SixLabors.ImageSharp.Size;
using ImgRectangle = SixLabors.ImageSharp.Rectangle;

namespace RonekaiImageFramer.Templates;

public sealed class InstagramSquareTemplate : TemplateBase
{
    private static BrandColorTheme Theme => BrandThemeContext.Current;

    public override string Id => "instagram-square";
    public override string Name => "Instagram Kare";
    public override string Description => "1080×1080 sosyal medya kare formatı.";
    public override ImgSize OutputSize => new(1080, 1080);

    protected override Image<Rgba32> Render(Image<Rgba32> source)
    {
        var canvas = CreateCanvas(OutputSize);
        int margin = (int)(OutputSize.Width * 0.06);
        var bounds = new ImgRectangle(margin, margin, OutputSize.Width - margin * 2, OutputSize.Height - margin * 2);
        DrawProductContained(canvas, source, bounds, Theme.Background);
        return canvas;
    }
}
