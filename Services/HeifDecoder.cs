using System.Windows.Media;
using System.Windows.Media.Imaging;
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
            using var stream = File.OpenRead(filePath);
            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);

            if (decoder.Frames.Count == 0)
                throw new InvalidOperationException("Dosyada görüntü karesi yok.");

            var frame = decoder.Frames[0];
            var converted = new FormatConvertedBitmap();
            converted.BeginInit();
            converted.Source = frame;
            converted.DestinationFormat = PixelFormats.Bgra32;
            converted.EndInit();
            converted.Freeze();

            int width = converted.PixelWidth;
            int height = converted.PixelHeight;
            int stride = width * 4;
            var pixels = new byte[height * stride];
            converted.CopyPixels(pixels, stride, 0);

            for (int i = 0; i < pixels.Length; i += 4)
                (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);

            return Image.LoadPixelData<Rgba32>(pixels, width, height);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"HEIC/HEIF okunamadı: {Path.GetFileName(filePath)}.\n\n{InstallHint}", ex);
        }
    }
}
