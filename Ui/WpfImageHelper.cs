using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace RonekaiImageFramer.Ui;

public static class WpfImageHelper
{
    public static BitmapSource ToBitmapSource(Image<Rgba32> image, int? maxWidth = null)
    {
        var png = EncodePng(image, maxWidth);
        return FromPngBytes(png);
    }

    public static byte[] EncodePng(Image<Rgba32> image, int? maxWidth = null)
    {
        Image<Rgba32> work = image;
        Image<Rgba32>? scaled = null;

        if (maxWidth.HasValue && image.Width > maxWidth.Value)
        {
            int w = maxWidth.Value;
            int h = Math.Max(1, (int)(image.Height * (w / (double)image.Width)));
            scaled = image.Clone(x => x.Resize(w, h));
            work = scaled;
        }

        try
        {
            using var ms = new MemoryStream();
            work.Save(ms, new PngEncoder());
            return ms.ToArray();
        }
        finally
        {
            scaled?.Dispose();
        }
    }

    /// <summary>PNG baytlarından WPF görüntüsü — UI iş parçacığında çağrılmalı.</summary>
    public static BitmapSource FromPngBytes(byte[] pngBytes)
    {
        using var ms = new MemoryStream(pngBytes);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = ms;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
