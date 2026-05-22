using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RonekaiImageFramer.Ui;

/// <summary>Önizleme görselinden (Uniform stretch) piksel rengi örnekler.</summary>
public static class PreviewColorSampler
{
    public static bool TryPick(BitmapSource? source, FrameworkElement host, Point positionInHost, out string hex)
    {
        hex = "#000000";
        if (source is null || host.ActualWidth <= 1 || host.ActualHeight <= 1)
            return false;

        var pixels = EnsureBgra32(source);
        double imgW = pixels.PixelWidth;
        double imgH = pixels.PixelHeight;
        double scale = Math.Min(host.ActualWidth / imgW, host.ActualHeight / imgH);
        double drawW = imgW * scale;
        double drawH = imgH * scale;
        double offsetX = (host.ActualWidth - drawW) / 2;
        double offsetY = (host.ActualHeight - drawH) / 2;

        double localX = positionInHost.X - offsetX;
        double localY = positionInHost.Y - offsetY;
        if (localX < 0 || localY < 0 || localX >= drawW || localY >= drawH)
            return false;

        int px = Math.Clamp((int)(localX / scale), 0, pixels.PixelWidth - 1);
        int py = Math.Clamp((int)(localY / scale), 0, pixels.PixelHeight - 1);

        int stride = pixels.PixelWidth * 4;
        var sample = new byte[4];
        pixels.CopyPixels(new Int32Rect(px, py, 1, 1), sample, stride, 0);
        hex = UiColorHelper.ToHex(sample[2], sample[1], sample[0]);
        return true;
    }

    private static BitmapSource EnsureBgra32(BitmapSource source)
    {
        if (source.Format == PixelFormats.Bgra32 || source.Format == PixelFormats.Pbgra32)
            return source;

        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        converted.Freeze();
        return converted;
    }
}
