using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace RonekaiImageFramer.Services;

/// <summary>Kaynak dosyayi ImageSharp Image{Rgba32} olarak yukler (PNG/JPEG/WEBP/AVIF/HEIC/SVG dahil).</summary>
public static class SourceImageLoader
{
    public static Image<Rgba32> Load(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Dosya bulunamadi.", filePath);

        long fileLength = new FileInfo(filePath).Length;
        if (fileLength == 0)
            throw new InvalidOperationException($"Dosya bos: {Path.GetFileName(filePath)}");

        var ext = Path.GetExtension(filePath);

        if (ImageInputCatalog.IsSvgExtension(ext))
            return SvgRasterizer.Load(filePath);

        // Uzantı .jpg/.jpeg olsa bile içerik AVIF/HEIF/WEBP olabilir
        bool jpegExtButNotJpeg = ImageInputCatalog.IsJpegExtension(ext)
                                 && !ImageInputCatalog.LooksLikeJpeg(filePath);
        if (jpegExtButNotJpeg && ImageInputCatalog.LooksLikeWebp(filePath))
            return LoadRaster(filePath);
        if (jpegExtButNotJpeg && ImageInputCatalog.LooksLikePng(filePath))
            return LoadRaster(filePath);

        bool heifFamily = ImageInputCatalog.IsAvifFile(filePath)
                          || ImageInputCatalog.IsHeifFile(filePath)
                          || ImageInputCatalog.IsHeifAliasExtension(ext)
                          || ImageInputCatalog.LooksLikeAvif(filePath)
                          || ImageInputCatalog.LooksLikeHeifContainer(filePath)
                          || jpegExtButNotJpeg;

        if (heifFamily)
            return LoadHeifFamily(filePath);

        try
        {
            return LoadRaster(filePath);
        }
        catch (Exception imageSharpEx) when (
            ShouldTryWpfFallback(imageSharpEx)
            || ImageInputCatalog.IsPngExtension(ext)
            || ImageInputCatalog.IsWebRasterExtension(ext))
        {
            try
            {
                return WpfBitmapDecoder.Load(filePath);
            }
            catch (Exception wpfEx)
            {
                // WEBP/AVIF WPF'de yoksa HEIF ailesi denensin
                if (ImageInputCatalog.LooksLikeHeifContainer(filePath)
                    || ImageInputCatalog.IsAvifExtension(ext))
                {
                    try
                    {
                        return HeifDecoder.Load(filePath);
                    }
                    catch (Exception heifEx)
                    {
                        throw CreateLoadException(filePath, fileLength, ext, imageSharpEx,
                            new AggregateException(wpfEx, heifEx));
                    }
                }

                throw CreateLoadException(filePath, fileLength, ext, imageSharpEx, wpfEx);
            }
        }
    }

    private static Image<Rgba32> LoadHeifFamily(string filePath)
    {
        try
        {
            return HeifDecoder.Load(filePath);
        }
        catch (Exception heifEx)
        {
            try
            {
                return LoadRaster(filePath);
            }
            catch (Exception rasterEx)
            {
                long len = new FileInfo(filePath).Length;
                throw CreateLoadException(filePath, len, Path.GetExtension(filePath), heifEx, rasterEx);
            }
        }
    }

    private static Image<Rgba32> LoadRaster(string filePath)
    {
        var image = Image.Load<Rgba32>(filePath);
        image.Mutate(ctx => ctx.AutoOrient());
        return image;
    }

    private static bool ShouldTryWpfFallback(Exception ex)
    {
        if (ex is UnknownImageFormatException or InvalidImageContentException or NotSupportedException)
            return true;

        var message = ex.Message;
        return message.Contains("decoders", StringComparison.OrdinalIgnoreCase)
               || message.Contains("cannot be loaded", StringComparison.OrdinalIgnoreCase)
               || message.Contains("not recognized", StringComparison.OrdinalIgnoreCase)
               || message.Contains("png", StringComparison.OrdinalIgnoreCase)
               || message.Contains("webp", StringComparison.OrdinalIgnoreCase)
               || message.Contains("avif", StringComparison.OrdinalIgnoreCase);
    }

    private static InvalidOperationException CreateLoadException(
        string filePath,
        long fileLength,
        string extension,
        Exception primaryEx,
        Exception fallbackEx)
    {
        string name = Path.GetFileName(filePath);
        string detected = ImageInputCatalog.DescribeDetectedFormat(filePath, fileLength);
        string sizeHint = fileLength < 4096
            ? $"{fileLength} bayt - muhtemelen kucuk onizleme/bozuk kopya"
            : $"{fileLength / 1024.0:0.#} KB";

        string extensionHint =
            ImageInputCatalog.IsJpegExtension(extension) && detected != "JPEG"
                ? "\n\nUzanti .jpg gorunuyor ancak dosya icerigi JPEG degil."
                : ImageInputCatalog.IsPngExtension(extension) && detected != "PNG"
                    ? "\n\nUzanti .png gorunuyor ancak dosya icerigi PNG degil."
                    : ImageInputCatalog.IsAvifExtension(extension)
                        ? "\n\nAVIF dosyasi okunamadi. libheif veya Windows AV1/HEIF eklentilerini kontrol edin."
                        : detected is "JPEG" or "PNG" or "WEBP" or "AVIF"
                            ? $"\n\nDosya {detected} gorunuyor ancak okunamadi - dosya bozuk olabilir."
                            : string.Empty;

        string message =
            $"Gorsel acilamadi: {name}\n" +
            $"Boyut: {sizeHint}\n" +
            $"Tespit edilen icerik: {detected}" +
            extensionHint;

        return new InvalidOperationException(message, new AggregateException(primaryEx, fallbackEx));
    }
}