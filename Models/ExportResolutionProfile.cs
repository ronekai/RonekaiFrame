namespace RonekaiImageFramer.Models;

public enum ExportSizeMode
{
    /// <summary>Şablonun tanımlı çıktı boyutu.</summary>
    TemplateDefault,

    /// <summary>Her dosyanın kendi piksel boyutu.</summary>
    SourceNative,

    /// <summary>Sabit genişlik × yükseklik (oran korunur, kenar boşluğu).</summary>
    Fixed,

    /// <summary>Uzun kenar üst sınırı (web / mağaza optimizasyonu).</summary>
    MaxLongEdge
}

public sealed class ExportResolutionProfile
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public ExportSizeMode Mode { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public int? MaxLongEdge { get; init; }

    public string SizeHint => Mode switch
    {
        ExportSizeMode.TemplateDefault => "Şablon boyutu",
        ExportSizeMode.SourceNative => "Dosya boyutuna göre",
        ExportSizeMode.MaxLongEdge => $"Uzun kenar ≤ {MaxLongEdge}px",
        ExportSizeMode.Fixed when Width.HasValue && Height.HasValue =>
            $"{Width} × {Height} px",
        _ => ""
    };
}
