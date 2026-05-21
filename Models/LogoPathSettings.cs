namespace RonekaiImageFramer.Models;

public sealed class LogoPathSettings
{
    public bool UseDefaultLogo { get; set; } = true;
    public string? CustomLogoPath { get; set; }

    public static LogoPathSettings CreateDefault() => new();
}
