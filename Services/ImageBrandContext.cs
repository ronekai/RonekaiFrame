using RonekaiImageFramer.Models;

namespace RonekaiImageFramer.Services;

public static class ImageBrandContext
{
    private static readonly AsyncLocal<ImageBrandSettings?> _current = new();

    public static ImageBrandSettings Current =>
        _current.Value ?? ImageBrandStore.Current;

    public static string MainText =>
        string.IsNullOrWhiteSpace(Current.MainText) ? "RONEKAI" : Current.MainText.Trim();

    public static string SuffixText => Current.SuffixText ?? "";

    public static IDisposable Use(ImageBrandSettings settings)
    {
        var previous = _current.Value;
        _current.Value = settings;
        return new Scope(previous);
    }

    private sealed class Scope(ImageBrandSettings? previous) : IDisposable
    {
        public void Dispose() => _current.Value = previous;
    }
}
