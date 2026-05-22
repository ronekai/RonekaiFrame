namespace RonekaiImageFramer.Templates;

/// <summary>Şablonun marka metnini (ana + ek) nereye çizeceği.</summary>
public enum TemplateBrandPlacement
{
    /// <summary>Şablon kendi marka alanını çizer (şerit, çapraz vb.).</summary>
    None,

    /// <summary>Sağ alt köşe filigran (ImageBrandContext metinleri).</summary>
    Corner,
}
