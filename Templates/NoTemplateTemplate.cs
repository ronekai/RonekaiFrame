using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ImgSize = SixLabors.ImageSharp.Size;

namespace RonekaiImageFramer.Templates;

/// <summary>Şablon uygulanmaz; logo, metin ve çıktı boyutu ayarları geçerlidir.</summary>
public sealed class NoTemplateTemplate : IProductTemplate
{
    public string Id => "sablon-yok";
    public string Name => "Şablon yok";
    public string Description => "Çerçeve veya stüdyo şablonu uygulanmaz. Logo ve çıktı boyutu isteğe bağlıdır.";
    public ImgSize OutputSize => new(1, 1);
    public bool IsPassthrough => true;
    public bool StretchToExport => false;

    public Image<Rgba32> Apply(Image<Rgba32> source) => source.CloneAs<Rgba32>();
}
