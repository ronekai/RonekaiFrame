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

    Image<Rgba32> Apply(Image<Rgba32> source);
}
