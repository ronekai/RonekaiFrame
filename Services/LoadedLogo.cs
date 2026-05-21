using RonekaiImageFramer.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace RonekaiImageFramer.Services;

public sealed class LoadedLogo : IDisposable
{
    public Image<Rgba32> Image { get; }
    public LogoFileKind Kind { get; }
    /// <summary>İşlemde kullanılan dosya yolu (PNG/JPEG kaynak veya dönüştürülmüş JPEG önbellek).</summary>
    public string EffectivePath { get; }
    public string FormatLabel { get; }

    public LoadedLogo(Image<Rgba32> image, LogoFileKind kind, string effectivePath, string formatLabel)
    {
        Image = image;
        Kind = kind;
        EffectivePath = effectivePath;
        FormatLabel = formatLabel;
    }

    public Image<Rgba32> CloneImage() => Image.CloneAs<Rgba32>();

    public void Dispose() => Image.Dispose();
}
