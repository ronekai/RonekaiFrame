namespace RonekaiImageFramer.Models;

/// <summary>Bir kaynak klasör için varsayılan ve dosya bazlı marka logo ayarları.</summary>
public sealed class SourceFolderLogoSettings
{
    public string? FolderPath { get; set; }
    /// <summary>İlk fotoğraf / tüm dosyalara uygulanan varsayılan.</summary>
    public FileBrandLogoOverride? FolderDefault { get; set; }
    public Dictionary<string, FileBrandLogoOverride> PerFile { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public SourceFolderLogoSettings Clone() => new()
    {
        FolderPath = FolderPath,
        FolderDefault = FolderDefault?.Clone(),
        PerFile = PerFile.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.Clone(),
            StringComparer.OrdinalIgnoreCase)
    };
}
