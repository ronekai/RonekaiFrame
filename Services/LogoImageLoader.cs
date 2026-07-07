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

    public const string OpenFileDialogFilter =
        "Logo dosyaları|*.png;*.jpg;*.jpeg;*.heic;*.heif;*.webp;*.bmp;*.svg|" +
        "PNG|*.png|JPEG|*.jpg;*.jpeg|SVG|*.svg|Mac HEIC|*.heic;*.heif|Tüm dosyalar|*.*";

    public const string HeaderLogoDialogFilter =
        "Görsel|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.svg|SVG|*.svg|Tüm dosyalar|*.*";

    public static string GetFormatLabelForPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "";

        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".png" => "PNG",
            ".svg" => "SVG (vektör, şeffaflık korunur)",
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

        if (ext == ".svg")
        {
            using var raster = SvgRasterizer.Load(fullPath);
            string svgCachePath = WritePngCache(fullPath, raster);
            var svgImage = Image.Load<Rgba32>(svgCachePath);
            return new LoadedLogo(svgImage, LogoFileKind.Svg, fullPath, "SVG");
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

    private static string WriteJpegCache(string sourcePath, Image<Rgba32> image) =>
        WriteImageCache(sourcePath, image, ".jpg",
            (img, path) => img.SaveAsJpeg(path, new JpegEncoder { Quality = 92 }));

    private static string WritePngCache(string sourcePath, Image<Rgba32> image) =>
        WriteImageCache(sourcePath, image, ".png", (img, path) => img.SaveAsPng(path));

    private static string WriteImageCache(
        string sourcePath,
        Image<Rgba32> image,
        string extension,
        Action<Image<Rgba32>, string> save)
    {
        Directory.CreateDirectory(LogoCacheFolder);
        var info = new FileInfo(sourcePath);
        string safeName = Path.GetFileNameWithoutExtension(sourcePath);
        foreach (var c in Path.GetInvalidFileNameChars())
            safeName = safeName.Replace(c, '_');

        string cacheFile = Path.Combine(
            LogoCacheFolder,
            $"{safeName}_{info.LastWriteTimeUtc.Ticks}{extension}");

        if (!File.Exists(cacheFile))
        {
            using var clone = image.CloneAs<Rgba32>();
            save(clone, cacheFile);
        }

        return cacheFile;
    }
}
