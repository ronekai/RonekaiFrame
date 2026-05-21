using RonekaiImageFramer.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ImgSize = SixLabors.ImageSharp.Size;

namespace RonekaiImageFramer.Templates;

public sealed class CornerWatermarkTemplate : TemplateBase
{
    public override string Id => "corner-watermark";
    public override string Name => "Köşe Filigran";
    public override string Description => "Orijinal oran + sağ alt RONEKAI.DEN.";
    public override ImgSize OutputSize => new(1600, 1600);

    protected override Image<Rgba32> Render(Image<Rgba32> source)
    {
        int maxEdge = 1600;
        var fit = CalculateFit(source.Size, new ImgSize(maxEdge, maxEdge));

        var canvas = source.Clone(ctx => ctx.Resize(new ResizeOptions
        {
            Size = fit,
            Mode = ResizeMode.Max,
            Sampler = KnownResamplers.Lanczos3
        }));

        BrandRenderer.DrawCornerWatermark(canvas);
        return canvas;
    }
}
