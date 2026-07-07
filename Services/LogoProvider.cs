using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace RonekaiImageFramer.Services;

public static class LogoProvider
{
    private static LoadedLogo? _cache;
    private static string? _cacheSourcePath;

    public static string AssetsFolder => Path.Combine(AppPaths.ProgramRoot, "Assets");

    public static string DefaultLogoPath => Path.Combine(AssetsFolder, "ronekai-logo.png");

    public static IReadOnlyList<string> DiscoverLogoCandidates()
    {
        var list = new List<string>();
        if (!Directory.Exists(AssetsFolder))
            return list;

        foreach (var ext in new[] { "*.png", "*.jpg", "*.jpeg", "*.svg", "*.webp", "*.heic", "*.heif" })
            list.AddRange(Directory.GetFiles(AssetsFolder, ext));

        return list.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p).ToList();
    }

    public static string? ResolveLogoPath(string? customPath)
    {
        if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
            return Path.GetFullPath(customPath);

        foreach (var candidate in new[]
                 {
                     DefaultLogoPath,
                     Path.Combine(AssetsFolder, "ronekai-logo.jpg"),
                     Path.Combine(AssetsFolder, "logo.png"),
                     Path.Combine(AssetsFolder, "logo.jpg"),
                 }.Concat(DiscoverLogoCandidates()))
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    public static LoadedLogo LoadDetails(string? customPath)
    {
        var path = ResolveLogoPath(customPath)
            ?? throw new FileNotFoundException(
                $"Logo bulunamadı. Lütfen logonuzu şuraya kopyalayın:\n{DefaultLogoPath}\n\n" +
                "veya arayüzden 'Logo seç' ile dosyayı gösterin.");

        if (_cache is not null &&
            string.Equals(_cacheSourcePath, path, StringComparison.OrdinalIgnoreCase))
        {
            return new LoadedLogo(
                _cache.Image.CloneAs<Rgba32>(),
                _cache.Kind,
                _cache.EffectivePath,
                _cache.FormatLabel);
        }

        _cache?.Dispose();
        _cacheSourcePath = path;
        _cache = LogoImageLoader.Load(path);
        return new LoadedLogo(
            _cache.CloneImage(),
            _cache.Kind,
            _cache.EffectivePath,
            _cache.FormatLabel);
    }

    public static Image<Rgba32> Load(string? customPath) => LoadDetails(customPath).CloneImage();

    public static void ClearCache()
    {
        _cache?.Dispose();
        _cache = null;
        _cacheSourcePath = null;
    }
}
