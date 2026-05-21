using RonekaiImageFramer.Models;

namespace RonekaiImageFramer.Services;

public static class ColorPackRegistry
{
    public static BrandColorTheme CustomPlaceholder { get; } =
        BrandColorTheme.CreateCustom("#F5F6F8", "#1B2A4A", "#C9A227");

    public static IReadOnlyList<ColorPackListItem> All { get; } =
    [
        new(
            new BrandColorTheme("klasik", "Klasik RONEKAI", "#F5F6F8", "#1B2A4A", "#C9A227"),
            "Açık gri zemin, lacivert RONEKAI, altın .DEN"),
        new(
            new BrandColorTheme("beyaz", "Beyaz Stüdyo", "#FFFFFF", "#1B2A4A", "#C9A227"),
            "Saf beyaz zemin, lacivert + altın yazı"),
        new(
            new BrandColorTheme("gece", "Gece Lacivert", "#0F1628", "#FFFFFF", "#C9A227"),
            "Koyu zemin, beyaz RONEKAI, altın .DEN"),
        new(
            new BrandColorTheme("antrasit", "Antrasit", "#2D3436", "#F5F6F8", "#C9A227"),
            "Koyu gri zemin, açık RONEKAI, altın .DEN"),
        new(
            new BrandColorTheme("altin", "Altın Vurgu", "#1B2A4A", "#C9A227", "#F5F6F8"),
            "Lacivert zemin, altın RONEKAI, açık .DEN"),
        new(
            new BrandColorTheme("gumus", "Gümüş Minimal", "#E8ECF2", "#2C3E50", "#7F8C9B"),
            "Gümüş zemin, koyu gri RONEKAI, gri .DEN"),
        new(CustomPlaceholder, "Özel renkler — Seç… ile zemin, RONEKAI ve .DEN ayarlanır."),
    ];

    public static ColorPackListItem? GetCustomItem() =>
        All.FirstOrDefault(p => p.Theme.IsCustom);
}
