using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace RonekaiImageFramer.Services;

/// <summary>
/// Canlı önizleme için decode edilmiş kaynak önbelleği (HEIC tekrar yüklemeyi önler).
/// </summary>
public static class PreviewSourceCache
{
    private static readonly object Gate = new();
    private static string? _path;
    private static long _mtimeUtcTicks;
    private static Image<Rgba32>? _image;

    public static Image<Rgba32> GetClone(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        string full = Path.GetFullPath(filePath);
        long mtime = File.GetLastWriteTimeUtc(full).Ticks;

        lock (Gate)
        {
            if (_image is not null
                && string.Equals(_path, full, StringComparison.OrdinalIgnoreCase)
                && _mtimeUtcTicks == mtime)
            {
                return _image.CloneAs<Rgba32>();
            }

            _image?.Dispose();
            _image = SourceImageLoader.Load(full);
            _path = full;
            _mtimeUtcTicks = mtime;
            return _image.CloneAs<Rgba32>();
        }
    }

    public static void Invalidate(string? filePath = null)
    {
        lock (Gate)
        {
            if (filePath is not null
                && _path is not null
                && !string.Equals(_path, Path.GetFullPath(filePath), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _image?.Dispose();
            _image = null;
            _path = null;
            _mtimeUtcTicks = 0;
        }
    }
}
