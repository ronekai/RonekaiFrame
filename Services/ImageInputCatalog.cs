namespace RonekaiImageFramer.Services;

/// <summary>Desteklenen kaynak dosya uzantilari (tek merkez).</summary>
public static class ImageInputCatalog
{
    public static readonly string[] NativeExtensions =
    [
        ".jpg", ".jpeg", ".jpe", ".jfif",
        ".png", ".webp", ".bmp", ".gif",
        ".tiff", ".tif", ".ico"
    ];

    /// <summary>Web / modern konteyner formatlari (AVIF, HEIF ailesi).</summary>
    public static readonly string[] AvifExtensions = [".avif", ".avifs"];

    /// <summary>iPhone / Mac (Apple) fotograf formatlari.</summary>
    public static readonly string[] HeifExtensions = [".heic", ".heif", ".hif"];

    /// <summary>HEIF benzeri veya yeniden adlandirilmis Apple fotograflari.</summary>
    public static readonly string[] HeifAliasExtensions = [".hdc"];

    /// <summary>SVG vektor (Svg.Skia ile rasterize).</summary>
    public static readonly string[] VectorExtensions = [".svg"];

    public static readonly string[] AllExtensions =
        NativeExtensions
            .Concat(AvifExtensions)
            .Concat(HeifExtensions)
            .Concat(HeifAliasExtensions)
            .Concat(VectorExtensions)
            .ToArray();

    public static string SupportedFormatsDescription =>
        "JPG/JFIF, PNG, WEBP, AVIF, BMP, GIF, TIFF, ICO, SVG, Mac/iPhone HEIC/HEIF ve .hdc";

    public static bool IsSupportedExtension(string extension) =>
        AllExtensions.Contains(extension.ToLowerInvariant());

    public static bool IsAvifExtension(string extension) =>
        AvifExtensions.Contains(extension.ToLowerInvariant());

    public static bool IsHeifExtension(string extension) =>
        HeifExtensions.Contains(extension.ToLowerInvariant());

    public static bool IsHeifAliasExtension(string extension) =>
        HeifAliasExtensions.Contains(extension.ToLowerInvariant());

    public static bool IsHeifFile(string filePath) =>
        IsHeifExtension(Path.GetExtension(filePath));

    public static bool IsAvifFile(string filePath) =>
        IsAvifExtension(Path.GetExtension(filePath));

    public static bool IsSvgExtension(string extension) =>
        extension.Equals(".svg", StringComparison.OrdinalIgnoreCase);

    public static bool IsHeifOrAliasFile(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return IsHeifExtension(ext) || IsHeifAliasExtension(ext);
    }

    public static bool IsHeifFamilyFile(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return IsHeifExtension(ext)
               || IsHeifAliasExtension(ext)
               || IsAvifExtension(ext)
               || LooksLikeHeifContainer(filePath);
    }

    public static bool IsJpegExtension(string extension)
    {
        var ext = extension.ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".jpe" or ".jfif";
    }

    public static bool IsPngExtension(string extension) =>
        extension.Equals(".png", StringComparison.OrdinalIgnoreCase);

    public static bool IsWebRasterExtension(string extension)
    {
        var ext = extension.ToLowerInvariant();
        return ext is ".webp" or ".avif" or ".avifs" or ".gif" or ".ico" or ".svg";
    }

    /// <summary>ISO BMFF / HEIF/AVIF konteyner imzasi (ftyp kutusu).</summary>
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

    public static bool LooksLikeAvif(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            Span<byte> header = stackalloc byte[32];
            int read = stream.Read(header);
            if (read < 12)
                return false;
            if (!(header[4] == (byte)'f' && header[5] == (byte)'t'
                  && header[6] == (byte)'y' && header[7] == (byte)'p'))
                return false;

            // brand: avif / avis / mif1+av01
            for (int i = 8; i <= read - 4; i++)
            {
                if (header[i] == (byte)'a' && header[i + 1] == (byte)'v'
                    && (header[i + 2] == (byte)'i' || header[i + 2] == (byte)'0')
                    && (header[i + 3] == (byte)'f' || header[i + 3] == (byte)'s' || header[i + 3] == (byte)'1'))
                    return true;
            }

            return IsAvifFile(filePath);
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

    public static bool LooksLikeWebp(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            Span<byte> header = stackalloc byte[12];
            if (stream.Read(header) < 12)
                return false;
            return header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F'
                   && header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P';
        }
        catch
        {
            return false;
        }
    }

    public static string DescribeDetectedFormat(string filePath, long fileLength)
    {
        if (fileLength == 0)
            return "bos dosya";

        try
        {
            using var stream = File.OpenRead(filePath);
            Span<byte> header = stackalloc byte[16];
            int read = stream.Read(header);
            if (read < 4)
                return "cok kisa / eksik veri";

            if (read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
                return "JPEG";

            if (read >= 8
                && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
                return "PNG";

            if (read >= 12
                && header[0] == (byte)'R' && header[1] == (byte)'I'
                && header[2] == (byte)'F' && header[3] == (byte)'F'
                && header[8] == (byte)'W' && header[9] == (byte)'E'
                && header[10] == (byte)'B' && header[11] == (byte)'P')
                return "WEBP";

            if (read >= 12
                && header[4] == (byte)'f' && header[5] == (byte)'t'
                && header[6] == (byte)'y' && header[7] == (byte)'p')
            {
                // Peek brand bytes 8-11
                var brand = System.Text.Encoding.ASCII.GetString(header.Slice(8, 4).ToArray());
                if (brand.StartsWith("avi", StringComparison.OrdinalIgnoreCase)
                    || brand.Contains("av01", StringComparison.OrdinalIgnoreCase))
                    return "AVIF";
                return "HEIF/HEIC/AVIF";
            }

            if (read >= 2 && header[0] == 0x42 && header[1] == 0x4D)
                return "BMP";

            if (read >= 4
                && ((header[0] == 0x49 && header[1] == 0x49) || (header[0] == 0x4D && header[1] == 0x4D))
                && header[2] == 0x2A && header[3] == 0x00)
                return "TIFF";

            if (read >= 4 && header[0] == 0x00 && header[1] == 0x00
                && (header[2] == 0x01 || header[2] == 0x02) && header[3] == 0x00)
                return "ICO";

            if (header[0] == (byte)'<' || (read >= 5 && header[0] == (byte)'<' && header[1] == (byte)'?'))
                return "SVG/XML veya metin";

            if (header[0] == (byte)'{' )
                return "metin veya web dosyasi (gercek fotograf degil)";

            if (read >= 3 && header[0] == 0xEF && header[1] == 0xBB && header[2] == 0xBF)
                return "metin dosyasi (UTF-8, gercek fotograf degil)";

            return "taninmayan ikili veri";
        }
        catch
        {
            return "okunamadi";
        }
    }
}