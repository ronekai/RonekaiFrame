namespace RonekaiImageFramer.Models;

public sealed class ColorPackListItem(BrandColorTheme theme, string description)
{
    public BrandColorTheme Theme { get; } = theme;
    public string Name => Theme.Name;
    public string Description { get; } = description;
}
