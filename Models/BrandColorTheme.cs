namespace RonekaiImageFramer.Models;

public sealed record BrandColorTheme(
    string Id,
    string Name,
    string BackgroundHex,
    string RonekaiHex,
    string DenHex,
    bool IsCustom = false)
{
    public static BrandColorTheme CreateCustom(string backgroundHex, string ronekaiHex, string denHex) =>
        new("ozel", "Özel (kendin seç)", backgroundHex, ronekaiHex, denHex, IsCustom: true);

}
