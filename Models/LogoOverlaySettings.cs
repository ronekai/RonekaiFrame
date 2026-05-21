namespace RonekaiImageFramer.Models;

public sealed record LogoOverlaySettings
{
    public LogoOverlayMode Mode { get; init; } = LogoOverlayMode.None;
    public float Opacity { get; init; } = 0.35f;
    public string? LogoFilePath { get; init; }

    public bool UsesLogo => Mode != LogoOverlayMode.None;

    public string ModeSuffix => Mode switch
    {
        LogoOverlayMode.Filigran => "filigran",
        LogoOverlayMode.ArkaPlan => "arkaplan",
        LogoOverlayMode.TamArkaPlan => "tam-arkaplan",
        LogoOverlayMode.Cerceve => "cerceve",
        LogoOverlayMode.RozetSagAlt => "rozet-sag",
        LogoOverlayMode.RozetSolAlt => "rozet-sol",
        LogoOverlayMode.MerkezRozet => "rozet-merkez",
        _ => "logo-yok"
    };

    public static float DefaultOpacity(LogoOverlayMode mode) => mode switch
    {
        LogoOverlayMode.Filigran => 0.22f,
        LogoOverlayMode.ArkaPlan => 0.18f,
        LogoOverlayMode.TamArkaPlan => 0.35f,
        LogoOverlayMode.Cerceve => 0.55f,
        LogoOverlayMode.RozetSagAlt or LogoOverlayMode.RozetSolAlt or LogoOverlayMode.MerkezRozet => 1f,
        _ => 0.35f
    };
}
