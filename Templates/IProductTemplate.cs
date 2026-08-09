using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ImgSize = SixLabors.ImageSharp.Size;

namespace RonekaiImageFramer.Templates;

public interface IProductTemplate
{
    string Id { get; }
    string Name { get; }
    string Description { get; }

    /// <summary>Varsayılan / minimum çıktı boyutu (liste etiketi).</summary>
    ImgSize OutputSize { get; }

    /// <summary>Kaynağa göre gerçek tuval boyutu (akıllı şablonlar uzun kenarı yuvarlar).</summary>
    ImgSize ResolveOutputSize(int sourceWidth, int sourceHeight);

    /// <summary>Çıktı boyutu kaynağa göre değişir.</summary>
    bool UsesSmartOutputSize { get; }

    /// <summary>Şablon çerçevesi uygulanmaz (ham görsel).</summary>
    bool IsPassthrough { get; }

    /// <summary>Seçili çıktı çözünürlüğüne tam yay (oran bozulabilir).</summary>
    bool StretchToExport { get; }

    Image<Rgba32> Apply(Image<Rgba32> source);
}
