namespace RonekaiImageFramer.Services;

/// <summary>Desteklenen kaynak dosya uzantıları (tek merkez).</summary>
public static class ImageInputCatalog
{
    public static readonly string[] NativeExtensions =
        [".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif", ".tiff", ".tif"];

    /// <summary>iPhone / Mac (Apple) fotoğraf formatları.</summary>
    public static readonly string[] HeifExtensions = [".heic", ".heif", ".hif"];

    /// <summary>HEIF benzeri veya yeniden adlandırılmış Apple fotoğrafları.</summary>
    public static readonly string[] HeifAliasExtensions = [".hdc"];

    public static readonly string[] AllExtensions =
        NativeExtensions.Concat(HeifExtensions).Concat(HeifAliasExtensions).ToArray();

    public static string SupportedFormatsDescription =>
        "JPG, PNG, WEBP, BMP, GIF, TIFF, Mac/iPhone HEIC/HEIF ve .hdc";

    public static bool IsSupportedExtension(string extension) =>
        AllExtensions.Contains(extension.ToLowerInvariant());

    public static bool IsHeifExtension(string extension) =>
        HeifExtensions.Contains(extension.ToLowerInvariant());

    public static bool IsHeifAliasExtension(string extension) =>
        HeifAliasExtensions.Contains(extension.ToLowerInvariant());

    public static bool IsHeifFile(string filePath) =>
        IsHeifExtension(Path.GetExtension(filePath));

    public static bool IsHeifOrAliasFile(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return IsHeifExtension(ext) || IsHeifAliasExtension(ext);
    }

    public static bool IsJpegExtension(string extension)
    {
        var ext = extension.ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".jpe";
    }

    /// <summary>ISO BMFF / HEIF konteyner imzası (ftyp kutusu).</summary>
    public static bool LooksLikeHeifContainer(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            Span<byte> header = stackalloc byte[12];
            if (stream.Read(header) < 12)
                return false;

            return header[4] == (byte)'f'
                   && header[5] == (byte)'t'
                   && header[6] == (byte)'y'
                   && header[7] == (byte)'p';
        }
        catch
        {
            return false;
        }
    }

    public static bool LooksLikeJpeg(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            Span<byte> header = stackalloc byte[3];
            if (stream.Read(header) < 3)
                return false;

            return header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        }
        catch
        {
            return false;
        }
    }

    public static string DescribeDetectedFormat(string filePath, long fileLength)
    {
        if (fileLength == 0)
            return "boş dosya";

        try
        {
            using var stream = File.OpenRead(filePath);
            Span<byte> header = stackalloc byte[16];
            int read = stream.Read(header);
            if (read < 4)
                return "çok kısa / eksik veri";

            if (read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
                return "JPEG";

            if (read >= 8
                && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
                return "PNG";

            if (read >= 12
                && header[4] == (byte)'f' && header[5] == (byte)'t'
                && header[6] == (byte)'y' && header[7] == (byte)'p')
                return "HEIF/HEIC";

            if (read >= 2 && header[0] == 0x42 && header[1] == 0x4D)
                return "BMP";

            if (read >= 4
                && ((header[0] == 0x49 && header[1] == 0x49) || (header[0] == 0x4D && header[1] == 0x4D))
                && header[2] == 0x2A && header[3] == 0x00)
                return "TIFF";

            if (header[0] == (byte)'<' || header[0] == (byte)'{')
                return "metin veya web dosyası (gerçek fotoğraf değil)";

            if (read >= 3 && header[0] == 0xEF && header[1] == 0xBB && header[2] == 0xBF)
                return "metin dosyası (UTF-8, gerçek fotoğraf değil)";

            return "tanınmayan ikili veri";
        }
        catch
        {
            return "okunamadı";
        }
    }
}
