using System.IO;
using System.Text.Json;
using RonekaiImageFramer.Models;

namespace RonekaiImageFramer.Services;

public static class ImageBrandStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static string SettingsPath =>
        Path.Combine(AppPaths.ProgramRoot, "image-brand.json");

    public static ImageBrandSettings Current { get; private set; } = Load();

    public static ImageBrandSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                Current = ImageBrandSettings.CreateDefault();
                return Current;
            }

            var json = File.ReadAllText(SettingsPath);
            Current = JsonSerializer.Deserialize<ImageBrandSettings>(json, JsonOptions)
                      ?? ImageBrandSettings.CreateDefault();
            return Current;
        }
        catch
        {
            Current = ImageBrandSettings.CreateDefault();
            return Current;
        }
    }

    public static void Save(ImageBrandSettings settings)
    {
        Current = settings.Clone();
        var json = JsonSerializer.Serialize(Current, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }
}
