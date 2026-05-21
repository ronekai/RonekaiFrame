namespace RonekaiImageFramer.Models;

public sealed class LogoModeListItem(LogoOverlayMode mode, string name, string description)
{
    public LogoOverlayMode Mode { get; } = mode;
    public string Name { get; } = name;
    public string Description { get; } = description;
}
