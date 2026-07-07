using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace RonekaiImageFramer.Services;

/// <summary>Mac/iPhone HEIC/HEIF — Windows WPF görüntü kodlayıcısı (HEIF eklentisi gerekir).</summary>
public static class HeifDecoder
{
    public const string InstallHint =
        "HEIC için Windows'a \"HEIF Image Extensions\" kurun:\n" +
        "Microsoft Store → HEIF Image Extensions\n" +
        "veya: ms-windows-store://pdp/?ProductId=9n4wgh0z6vhq";

    public static Image<Rgba32> Load(string filePath)
    {
        try
        {
            return WpfBitmapDecoder.Load(filePath);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"HEIC/HEIF okunamadı: {Path.GetFileName(filePath)}.\n\n{InstallHint}", ex);
        }
    }
}
