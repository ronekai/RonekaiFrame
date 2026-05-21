namespace RonekaiImageFramer.Models;

public sealed class HeaderBrandingSettings
{
    public HeaderBrandingMode Mode { get; set; } = HeaderBrandingMode.Text;
    public string MainText { get; set; } = "RONEKAI";
    public string SuffixText { get; set; } = ".DEN";
    public string Tagline { get; set; } = "PhonixFrame — toplu e-ticaret görsel stüdyo";
    public string? LogoPath { get; set; }

    public static HeaderBrandingSettings CreateDefault() => new();
}
