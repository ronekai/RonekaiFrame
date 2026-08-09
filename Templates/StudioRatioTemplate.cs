using RonekaiImageFramer.Models;
using RonekaiImageFramer.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ImgSize = SixLabors.ImageSharp.Size;
using ImgRectangle = SixLabors.ImageSharp.Rectangle;

namespace RonekaiImageFramer.Templates;

/// <summary>
/// Oranlı stüdyo şablonu: ürün bozulmadan ortalanır; boşluklar renk paleti zemininden.
/// Büyük kaynaklarda tuval, seçilen orana en yakın “güzel” boyuta (100 px) yükseltilir.
/// </summary>
public sealed class StudioRatioTemplate : TemplateBase
{
    private readonly int _ratioW;
    private readonly int _ratioH;

    public StudioRatioTemplate(
        string id,
        string name,
        string description,
        int width,
        int height,
        bool blackBackground)
    {
        Id = id;
        Name = name;
        Description = description;
        OutputSize = new ImgSize(width, height);
        PrefersDarkPalette = blackBackground;

        int g = Gcd(width, height);
        _ratioW = Math.Max(1, width / g);
        _ratioH = Math.Max(1, height / g);
    }

    public override string Id { get; }
    public override string Name { get; }
    public override string Description { get; }
    public override ImgSize OutputSize { get; }

    /// <summary>Siyah varyant — açılışta koyu palet önerilir; zemin yine paletten gelir.</summary>
    public bool PrefersDarkPalette { get; }

    public override bool UsesSmartOutputSize => true;

    public override ImgSize ResolveOutputSize(int sourceWidth, int sourceHeight) =>
        SmartCanvasSize.Resolve(
            sourceWidth,
            sourceHeight,
            _ratioW,
            _ratioH,
            OutputSize.Width,
            OutputSize.Height);

    protected override TemplateBrandPlacement BrandTextPlacement => TemplateBrandPlacement.None;

    protected override Image<Rgba32> Render(Image<Rgba32> source)
    {
        var size = ResolveOutputSize(source.Width, source.Height);
        var canvas = CreateCanvas(size);
        var bounds = new ImgRectangle(0, 0, size.Width, size.Height);
        DrawProductContained(canvas, source, bounds, ThemeColorSlot.Background, fillEntireCanvas: true);
        return canvas;
    }

    private static int Gcd(int a, int b)
    {
        while (b != 0)
        {
            int t = a % b;
            a = b;
            b = t;
        }
        return Math.Max(1, Math.Abs(a));
    }
}
