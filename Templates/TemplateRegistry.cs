namespace RonekaiImageFramer.Templates;

public static class TemplateRegistry
{
    private static readonly IProductTemplate[] All =
    [
        new NoTemplateTemplate(),
        new YayTemplate(),

        // Shopier 1:1 — büyük kaynakta örn. 2384×2200 → 2400×2400
        new StudioRatioTemplate(
            "shopier-beyaz",
            "Shopier Beyaz",
            "Shopier 1:1. Büyük görsellerde kareye en yakın boyuta yükseltilir; zemin renk paletinden (beyaz önerilir).",
            1200, 1200,
            blackBackground: false),
        new StudioRatioTemplate(
            "shopier-siyah",
            "Shopier Siyah",
            "Shopier 1:1. Büyük görsellerde kareye en yakın boyuta yükseltilir; zemin renk paletinden (koyu önerilir).",
            1200, 1200,
            blackBackground: true),

        // Web 4:3
        new StudioRatioTemplate(
            "web-beyaz",
            "Web Beyaz",
            "Web 4:3. Büyük görsellerde 4:3’e en yakın boyuta yükseltilir; zemin renk paletinden (beyaz önerilir).",
            1600, 1200,
            blackBackground: false),
        new StudioRatioTemplate(
            "web-siyah",
            "Web Siyah",
            "Web 4:3. Büyük görsellerde 4:3’e en yakın boyuta yükseltilir; zemin renk paletinden (koyu önerilir).",
            1600, 1200,
            blackBackground: true),

        // Instagram 4:5
        new StudioRatioTemplate(
            "instagram-beyaz",
            "Instagram Beyaz",
            "Instagram 4:5. Büyük görsellerde 4:5’e en yakın boyuta yükseltilir; zemin renk paletinden (beyaz önerilir).",
            1080, 1350,
            blackBackground: false),
        new StudioRatioTemplate(
            "instagram-siyah",
            "Instagram Siyah",
            "Instagram 4:5. Büyük görsellerde 4:5’e en yakın boyuta yükseltilir; zemin renk paletinden (koyu önerilir).",
            1080, 1350,
            blackBackground: true),
    ];

    public static IReadOnlyList<IProductTemplate> Templates => All;

    public static IProductTemplate? GetById(string id) =>
        All.FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}
