using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ImgSize = SixLabors.ImageSharp.Size;

namespace RonekaiImageFramer.Templates;

/// <summary>Görseli seçili platform çıktı boyutuna tam yayar (kenar boşluğu yok).</summary>
public sealed class YayTemplate : IProductTemplate
{
    public string Id => "yay";
    public string Name => "Yay";
    public string Description =>
        "Görsel seçili çıktı çözünürlüğü / platform boyutuna tam yayılır (oran bozulabilir). " +
        "Instagram, Trendyol vb. sabit boyut seçmeniz önerilir.";
    public ImgSize OutputSize => new(1, 1);
    public ImgSize ResolveOutputSize(int sourceWidth, int sourceHeight) =>
        new(Math.Max(1, sourceWidth), Math.Max(1, sourceHeight));
    public bool UsesSmartOutputSize => false;
    public bool IsPassthrough => true;
    public bool StretchToExport => true;

    public Image<Rgba32> Apply(Image<Rgba32> source) => source.CloneAs<Rgba32>();
}
