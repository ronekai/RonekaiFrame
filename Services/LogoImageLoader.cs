using RonekaiImageFramer.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace RonekaiImageFramer.Services;

public static class LogoImageLoader
{
    public static string LogoCacheFolder =>
        Path.Combine(AppPaths.ProgramRoot, "Assets", ".logo-cache");

    public static string GetFormatLabelForPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "";

        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".png" => "PNG",
            ".jpg" or ".jpeg" => "JPEG",
            ".heic" or ".heif" or ".hif" => "JPEG (Mac HEIC dönüştürülür)",
            ".webp" or ".bmp" or ".gif" or ".tif" or ".tiff" => "JPEG (dönüştürülür)",
            _ => "JPEG (dönüştürülür)"
        };
    }

    public static LoadedLogo Load(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Logo dosyası bulunamadı.", filePath);

        var fullPath = Path.GetFullPath(filePath);
        var ext = Path.GetExtension(fullPath).ToLowerInvariant();

        if (ext == ".png")
        {
            var png = Image.Load<Rgba32>(fullPath);
            return new LoadedLogo(png, LogoFileKind.Png, fullPath, "PNG");
        }

        if (ext is ".jpg" or ".jpeg")
        {
            var jpg = Image.Load<Rgba32>(fullPath);
            jpg.Mutate(ctx => ctx.AutoOrient());
            return new LoadedLogo(jpg, LogoFileKind.Jpeg, fullPath, "JPEG");
        }

        using var source = LoadSourcePixels(fullPath, ext);
        source.Mutate(ctx => ctx.AutoOrient());
        string cachePath = WriteJpegCache(fullPath, source);
        var cached = Image.Load<Rgba32>(cachePath);
        return new LoadedLogo(cached, LogoFileKind.ConvertedJpeg, cachePath, "JPEG (dönüştürüldü)");
    }

    private static Image<Rgba32> LoadSourcePixels(string fullPath, string ext)
    {
        if (ImageInputCatalog.IsHeifExtension(ext))
            return HeifDecoder.Load(fullPath);

        return Image.Load<Rgba32>(fullPath);
    }

    private static string WriteJpegCache(string sourcePath, Image<Rgba32> image)
    {
        Directory.CreateDirectory(LogoCacheFolder);
        var info = new FileInfo(sourcePath);
        string safeName = Path.GetFileNameWithoutExtension(sourcePath);
        foreach (var c in Path.GetInvalidFileNameChars())
            safeName = safeName.Replace(c, '_');

        string cacheFile = Path.Combine(
            LogoCacheFolder,
            $"{safeName}_{info.LastWriteTimeUtc.Ticks}.jpg");

        if (!File.Exists(cacheFile))
        {
            using var clone = image.CloneAs<Rgba32>();
            clone.SaveAsJpeg(cacheFile, new JpegEncoder { Quality = 92 });
        }

        return cacheFile;
    }
}
