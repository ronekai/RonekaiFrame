using RonekaiImageFramer.Models;

namespace RonekaiImageFramer.Services;

public static class BrandThemeContext
{
    private static readonly AsyncLocal<BrandColorTheme?> _current = new();

    public static BrandColorTheme Current =>
        _current.Value ?? ColorPackRegistry.All[0].Theme;

    public static IDisposable Use(BrandColorTheme theme)
    {
        var previous = _current.Value;
        _current.Value = theme;
        return new Scope(previous);
    }

    private sealed class Scope(BrandColorTheme? previous) : IDisposable
    {
        public void Dispose() => _current.Value = previous;
    }
}
