namespace RonekaiImageFramer.Services;

/// <summary>Desteklenen kaynak dosya uzantıları (tek merkez).</summary>
public static class ImageInputCatalog
{
    public static readonly string[] NativeExtensions =
        [".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif", ".tiff", ".tif"];

    /// <summary>iPhone / Mac (Apple) fotoğraf formatları.</summary>
    public static readonly string[] HeifExtensions = [".heic", ".heif", ".hif"];

    public static readonly string[] AllExtensions =
        NativeExtensions.Concat(HeifExtensions).ToArray();

    public static string SupportedFormatsDescription =>
        "JPG, PNG, WEBP, BMP, GIF, TIFF ve Mac/iPhone HEIC/HEIF";

    public static bool IsSupportedExtension(string extension) =>
        AllExtensions.Contains(extension.ToLowerInvariant());

    public static bool IsHeifExtension(string extension) =>
        HeifExtensions.Contains(extension.ToLowerInvariant());

    public static bool IsHeifFile(string filePath) =>
        IsHeifExtension(Path.GetExtension(filePath));
}
