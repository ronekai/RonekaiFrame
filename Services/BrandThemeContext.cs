using RonekaiImageFramer.Models;

namespace RonekaiImageFramer.Services;

public static class BrandThemeContext
{
    private static readonly AsyncLocal<BrandColorTheme?> _current = new();
    private static readonly AsyncLocal<ThemeColorSet?> _appearance = new();

    public static BrandColorTheme Current =>
        _current.Value ?? ColorPackRegistry.All[0].Theme;

    public static ThemeColorSet Appearance =>
        _appearance.Value ?? ThemeColorSet.FromTheme(Current);

    public static IDisposable Use(BrandColorTheme theme, ThemeColorSet? appearance = null)
    {
        var previousTheme = _current.Value;
        var previousAppearance = _appearance.Value;
        _current.Value = theme;
        _appearance.Value = appearance ?? ThemeColorSet.FromTheme(theme);
        return new Scope(previousTheme, previousAppearance);
    }

    private sealed class Scope(BrandColorTheme? previousTheme, ThemeColorSet? previousAppearance) : IDisposable
    {
        public void Dispose()
        {
            _current.Value = previousTheme;
            _appearance.Value = previousAppearance;
        }
    }
}
