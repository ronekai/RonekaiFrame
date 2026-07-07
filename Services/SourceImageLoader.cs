using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace RonekaiImageFramer.Services;

/// <summary>Kaynak dosyayı ImageSharp <see cref="Image{Rgba32}"/> olarak yükler (HEIC/HEIF dahil).</summary>
public static class SourceImageLoader
{
    public static Image<Rgba32> Load(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Dosya bulunamadı.", filePath);

        long fileLength = new FileInfo(filePath).Length;
        if (fileLength == 0)
            throw new InvalidOperationException($"Dosya boş: {Path.GetFileName(filePath)}");

        var ext = Path.GetExtension(filePath);

        if (ImageInputCatalog.IsHeifFile(filePath) || ImageInputCatalog.IsHeifAliasExtension(ext))
            return LoadHeifPath(filePath);

        if (ImageInputCatalog.LooksLikeHeifContainer(filePath))
            return HeifDecoder.Load(filePath);

        try
        {
            return LoadRaster(filePath);
        }
        catch (Exception imageSharpEx) when (ShouldTryWpfFallback(imageSharpEx))
        {
            try
            {
                return WpfBitmapDecoder.Load(filePath);
            }
            catch (Exception wpfEx)
            {
                throw CreateLoadException(filePath, fileLength, ext, imageSharpEx, wpfEx);
            }
        }
    }

    private static Image<Rgba32> LoadHeifPath(string filePath)
    {
        if (ImageInputCatalog.LooksLikeHeifContainer(filePath) || ImageInputCatalog.IsHeifFile(filePath))
            return HeifDecoder.Load(filePath);

        try
        {
            return LoadRaster(filePath);
        }
        catch (Exception rasterEx) when (ShouldTryWpfFallback(rasterEx))
        {
            try
            {
                return HeifDecoder.Load(filePath);
            }
            catch (Exception heifEx)
            {
                long len = new FileInfo(filePath).Length;
                throw CreateLoadException(filePath, len, Path.GetExtension(filePath), rasterEx, heifEx);
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
               || message.Contains("not recognized", StringComparison.OrdinalIgnoreCase);
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
            ? $"{fileLength} bayt — muhtemelen küçük önizleme/bozuk kopya"
            : $"{fileLength / 1024.0:0.#} KB";

        string extensionHint = ImageInputCatalog.IsJpegExtension(extension) && detected != "JPEG"
            ? "\n\nUzantı .jpg görünüyor ancak dosya içeriği JPEG değil. " +
              "OneDrive/iCloud eşitlemesi tamamlanmamış veya yanlış dosya kopyalanmış olabilir. " +
              "Orijinal fotoğrafı klasöre yeniden kopyalayın."
            : detected == "JPEG"
                ? "\n\nDosya JPEG görünüyor ancak okunamadı — dosya bozuk olabilir."
                : string.Empty;

        string message =
            $"Görsel açılamadı: {name}\n" +
            $"Boyut: {sizeHint}\n" +
            $"Tespit edilen içerik: {detected}" +
            extensionHint;

        return new InvalidOperationException(message, new AggregateException(primaryEx, fallbackEx));
    }
}
