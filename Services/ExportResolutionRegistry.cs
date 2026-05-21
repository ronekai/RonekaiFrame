using RonekaiImageFramer.Models;

namespace RonekaiImageFramer.Services;

public sealed class ExportResolutionListItem
{
    public ExportResolutionProfile Profile { get; }
    public int? TemplateWidth { get; }
    public int? TemplateHeight { get; }

    public ExportResolutionListItem(
        ExportResolutionProfile profile,
        int? templateWidth = null,
        int? templateHeight = null)
    {
        Profile = profile;
        TemplateWidth = templateWidth;
        TemplateHeight = templateHeight;
    }

    public string Name => ExportResolutionRegistry.FormatDisplayName(Profile, TemplateWidth, TemplateHeight);

    public string Description => $"{Profile.Description} — {Profile.SizeHint}";
}

public static class ExportResolutionRegistry
{
    private static readonly ExportResolutionProfile[] Profiles =
    [
        new()
        {
            Id = "template-default",
            Name = "Şablon boyutu (önerilen)",
            Description = "Seçili şablonun standart çıktı çözünürlüğü.",
            Mode = ExportSizeMode.TemplateDefault
        },
        new()
        {
            Id = "source-native",
            Name = "Kaynak dosya boyutu",
            Description = "Her fotoğraf kendi orijinal piksel boyutunda kaydedilir.",
            Mode = ExportSizeMode.SourceNative
        },
        new()
        {
            Id = "instagram-square",
            Name = "Instagram — Kare gönderi",
            Description = "Akış ve kare vitrin.",
            Mode = ExportSizeMode.Fixed,
            Width = 1080,
            Height = 1080
        },
        new()
        {
            Id = "instagram-portrait",
            Name = "Instagram — Dikey 4:5",
            Description = "Akışta daha fazla alan kaplayan format.",
            Mode = ExportSizeMode.Fixed,
            Width = 1080,
            Height = 1350
        },
        new()
        {
            Id = "instagram-story",
            Name = "Instagram — Hikaye / Reels kapak",
            Description = "Hikaye ve dikey video kapakları.",
            Mode = ExportSizeMode.Fixed,
            Width = 1080,
            Height = 1920
        },
        new()
        {
            Id = "whatsapp-square",
            Name = "WhatsApp — Kare / profil",
            Description = "Durum önizleme ve kare paylaşım.",
            Mode = ExportSizeMode.Fixed,
            Width = 1080,
            Height = 1080
        },
        new()
        {
            Id = "whatsapp-status",
            Name = "WhatsApp — Durum (dikey)",
            Description = "Tam ekran durum görseli.",
            Mode = ExportSizeMode.Fixed,
            Width = 1080,
            Height = 1920
        },
        new()
        {
            Id = "sahibinden-gallery",
            Name = "Sahibinden — Galeri 4:3",
            Description = "İlan galerisi için yaygın 4:3 oran.",
            Mode = ExportSizeMode.Fixed,
            Width = 1024,
            Height = 768
        },
        new()
        {
            Id = "sahibinden-hd",
            Name = "Sahibinden — HD vitrin",
            Description = "Kapak ve öne çıkan ilan görselleri.",
            Mode = ExportSizeMode.Fixed,
            Width = 1600,
            Height = 1200
        },
        new()
        {
            Id = "facebook-feed",
            Name = "Facebook — Paylaşım",
            Description = "Sayfa ve mağaza gönderi önerisi.",
            Mode = ExportSizeMode.Fixed,
            Width = 1200,
            Height = 628
        },
        new()
        {
            Id = "linkedin-post",
            Name = "LinkedIn — Gönderi",
            Description = "Profesyonel ağ paylaşımı.",
            Mode = ExportSizeMode.Fixed,
            Width = 1200,
            Height = 627
        },
        new()
        {
            Id = "google-merchant",
            Name = "Google Alışveriş / Merchant",
            Description = "Ürün akışı için yüksek çözünürlük.",
            Mode = ExportSizeMode.Fixed,
            Width = 1500,
            Height = 1500
        },
        new()
        {
            Id = "ecommerce-pro",
            Name = "E-ticaret Pro",
            Description = "Trendyol, Hepsiburada, N11 vitrin standardı.",
            Mode = ExportSizeMode.Fixed,
            Width = 2000,
            Height = 2000
        },
        new()
        {
            Id = "web-optimized",
            Name = "Web optimize",
            Description = "Site ve katalog için hızlı yükleme.",
            Mode = ExportSizeMode.MaxLongEdge,
            MaxLongEdge = 1200
        },
        new()
        {
            Id = "amazon-product",
            Name = "Amazon — Ürün görseli",
            Description = "Beyaz zeminli pazar yeri ürün kuralı.",
            Mode = ExportSizeMode.Fixed,
            Width = 2000,
            Height = 2000
        },
    ];

    public static IReadOnlyList<ExportResolutionListItem> All =>
        BuildListItems();

    public static IReadOnlyList<ExportResolutionListItem> BuildListItems(
        int? templateWidth = null,
        int? templateHeight = null) =>
        Profiles.Select(p => new ExportResolutionListItem(p, templateWidth, templateHeight)).ToList();

    public static string FormatDisplayName(
        ExportResolutionProfile profile,
        int? templateWidth = null,
        int? templateHeight = null)
    {
        string suffix = profile.Mode switch
        {
            ExportSizeMode.TemplateDefault when templateWidth > 0 && templateHeight > 0 =>
                $"({templateWidth}×{templateHeight} px)",
            ExportSizeMode.TemplateDefault => "(şablona göre)",
            ExportSizeMode.SourceNative => "(dosyaya göre)",
            ExportSizeMode.MaxLongEdge => $"(uzun kenar ≤ {profile.MaxLongEdge} px)",
            ExportSizeMode.Fixed when profile.Width > 0 && profile.Height > 0 =>
                $"({profile.Width}×{profile.Height} px)",
            _ => ""
        };

        return string.IsNullOrEmpty(suffix) ? profile.Name : $"{profile.Name} {suffix}";
    }

    public static ExportResolutionProfile Default => Profiles[0];

    public static ExportResolutionProfile? GetById(string id) =>
        Profiles.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}
