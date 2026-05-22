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
            "Açık gri zemin, lacivert ana metin, altın ek metin"),
        new(
            new BrandColorTheme("beyaz", "Beyaz Stüdyo", "#FFFFFF", "#1B2A4A", "#C9A227"),
            "Saf beyaz zemin, lacivert + altın yazı"),
        new(
            new BrandColorTheme("gece", "Gece Lacivert", "#0F1628", "#FFFFFF", "#C9A227"),
            "Koyu zemin, beyaz ana metin, altın ek metin"),
        new(
            new BrandColorTheme("antrasit", "Antrasit", "#2D3436", "#F5F6F8", "#C9A227"),
            "Koyu gri zemin, açık RONEKAI, altın .DEN"),
        new(
            new BrandColorTheme("altin", "Altın Vurgu", "#1B2A4A", "#C9A227", "#F5F6F8"),
            "Lacivert zemin, altın ana metin, açık ek metin"),
        new(
            new BrandColorTheme("gumus", "Gümüş Minimal", "#E8ECF2", "#2C3E50", "#7F8C9B"),
            "Gümüş zemin, koyu gri ana metin, gri ek metin"),
        new(
            new BrandColorTheme("pazaryeri-turuncu", "Pazaryeri Turuncu", "#FFFFFF", "#F27A1A", "#1B2A4A"),
            "Beyaz zemin, turuncu marka vurgusu"),
        new(
            new BrandColorTheme("sahibinden", "İlan Sarı Vurgu", "#FFF9E6", "#1A1A1A", "#FFD100"),
            "Açık sarı zemin, siyah + sarı vurgu"),
        new(
            new BrandColorTheme("pastel-rose", "Pastel Gül", "#FFF0F3", "#9E4770", "#D4A5A5"),
            "Moda / kozmetik pastel pembe"),
        new(
            new BrandColorTheme("dogal-yesil", "Doğal Yeşil", "#F4F7F0", "#2D5A27", "#8BA888"),
            "Organik ürün, doğa tonları"),
        new(
            new BrandColorTheme("monokrom", "Monokrom S/B", "#FFFFFF", "#111111", "#666666"),
            "Sade siyah-beyaz vitrin"),
        new(
            new BrandColorTheme("mercan", "Mercan Canlı", "#FFFFFF", "#FF6B6B", "#1B2A4A"),
            "Yaz koleksiyonu, canlı mercan"),
        new(
            new BrandColorTheme("lavanta", "Lavanta", "#F3EEFA", "#5B4B8A", "#B8A9C9"),
            "Kozmetik / kişisel bakım"),
        new(
            new BrandColorTheme("buz-mavi", "Buz Mavi Teknoloji", "#F0F7FF", "#0A6EBD", "#5BA4CF"),
            "Elektronik / teknoloji ürünleri"),
        new(
            new BrandColorTheme("sicak-bej", "Sıcak Bej", "#F7F3EE", "#5C4A3A", "#C9A227"),
            "Mobilya / dekorasyon"),
        new(
            new BrandColorTheme("gece-altin", "Gece & Altın", "#121212", "#D4AF37", "#F5F5F5"),
            "Lüks segment, altın detay"),
        new(
            new BrandColorTheme("mint-fresh", "Nane Ferahlık", "#E8F8F5", "#1B7F6E", "#7DCEC4"),
            "Temizlik / taze ürün hissi"),
        new(CustomPlaceholder, "Özel renkler — zemin, ana metin ve ek metin için Hex/RGB, Seç… veya Damla."),
    ];

    public static ColorPackListItem? GetCustomItem() =>
        All.FirstOrDefault(p => p.Theme.IsCustom);
}
