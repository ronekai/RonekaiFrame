namespace RonekaiImageFramer.Models;

public sealed class ThemeColorAppearance
{
    public string PrimaryHex { get; set; } = "#F5F6F8";
    public string GradientEndHex { get; set; } = "#E8ECF2";
    public ColorFillMode FillMode { get; set; } = ColorFillMode.Solid;
    public float Opacity { get; set; } = 1f;
    public GradientDirection GradientDirection { get; set; } = GradientDirection.Vertical;

    public ThemeColorAppearance Clone() => new()
    {
        PrimaryHex = PrimaryHex,
        GradientEndHex = GradientEndHex,
        FillMode = FillMode,
        Opacity = Opacity,
        GradientDirection = GradientDirection
    };

    public static ThemeColorAppearance FromHex(string hex, string? gradientEnd = null) => new()
    {
        PrimaryHex = hex,
        GradientEndHex = gradientEnd ?? hex,
        FillMode = ColorFillMode.Solid,
        Opacity = 1f
    };
}
