using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ImgSize = SixLabors.ImageSharp.Size;

namespace RonekaiImageFramer.Templates;

public interface IProductTemplate
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    ImgSize OutputSize { get; }

    /// <summary>Şablon çerçevesi uygulanmaz (ham görsel).</summary>
    bool IsPassthrough { get; }

    /// <summary>Seçili çıktı çözünürlüğüne tam yay (oran bozulabilir).</summary>
    bool StretchToExport { get; }

    Image<Rgba32> Apply(Image<Rgba32> source);
}
