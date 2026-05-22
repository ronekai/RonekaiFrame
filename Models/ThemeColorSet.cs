namespace RonekaiImageFramer.Models;

public sealed class ThemeColorSet
{
    public ThemeColorAppearance Background { get; set; } = ThemeColorAppearance.FromHex("#F5F6F8");
    public ThemeColorAppearance MainText { get; set; } = ThemeColorAppearance.FromHex("#1B2A4A");
    public ThemeColorAppearance Suffix { get; set; } = ThemeColorAppearance.FromHex("#C9A227");

    public ThemeColorAppearance Get(ThemeColorSlot slot) => slot switch
    {
        ThemeColorSlot.MainText => MainText,
        ThemeColorSlot.Suffix => Suffix,
        _ => Background
    };

    public ThemeColorSet Clone() => new()
    {
        Background = Background.Clone(),
        MainText = MainText.Clone(),
        Suffix = Suffix.Clone()
    };

    public static ThemeColorSet FromHex(string backgroundHex, string mainTextHex, string suffixHex) => new()
    {
        Background = ThemeColorAppearance.FromHex(backgroundHex),
        MainText = ThemeColorAppearance.FromHex(mainTextHex),
        Suffix = ThemeColorAppearance.FromHex(suffixHex)
    };

    public static ThemeColorSet FromTheme(BrandColorTheme theme) =>
        FromHex(theme.BackgroundHex, theme.RonekaiHex, theme.DenHex);

    public void SyncPrimaryHexFrom(BrandColorTheme theme)
    {
        Background.PrimaryHex = theme.BackgroundHex;
        Background.GradientEndHex = theme.BackgroundHex;
        MainText.PrimaryHex = theme.RonekaiHex;
        MainText.GradientEndHex = theme.RonekaiHex;
        Suffix.PrimaryHex = theme.DenHex;
        Suffix.GradientEndHex = theme.DenHex;
    }
}
