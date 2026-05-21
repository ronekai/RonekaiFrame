using RonekaiImageFramer.Models;

namespace RonekaiImageFramer.Services;

public static class LogoModeRegistry
{
    public static IReadOnlyList<LogoModeListItem> All { get; } =
    [
        new(LogoOverlayMode.None, "Logo yok",
            "Sadece seçilen şablon uygulanır."),
        new(LogoOverlayMode.Filigran, "Filigran (ortada şeffaf)",
            "Logonuz görselin ortasında yarı şeffaf filigran olarak görünür."),
        new(LogoOverlayMode.ArkaPlan, "Arka plan (soluk, bulanık)",
            "Logo ürünün arkasında soluk ve hafif bulanık zemin olur."),
        new(LogoOverlayMode.TamArkaPlan, "Tam arka plan",
            "Logo tüm zemini kaplar; ürün önde kalır."),
        new(LogoOverlayMode.Cerceve, "Çerçeve (kenar bantları)",
            "Logo üst, alt ve yan kenarlarda çerçeve gibi yerleşir."),
        new(LogoOverlayMode.RozetSagAlt, "Rozet — sağ alt",
            "Logo sağ alt köşede rozet / yapışkan gibi durur."),
        new(LogoOverlayMode.RozetSolAlt, "Rozet — sol alt",
            "Logo sol alt köşede rozet olarak durur."),
        new(LogoOverlayMode.MerkezRozet, "Rozet — alt orta",
            "Logo altta ortada küçük rozet olarak durur."),
    ];
}
