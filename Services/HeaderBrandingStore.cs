using System.IO;
using System.Text.Json;
using RonekaiImageFramer.Models;

namespace RonekaiImageFramer.Services;

public static class HeaderBrandingStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static string SettingsPath =>
        Path.Combine(AppPaths.ProgramRoot, "header-branding.json");

    public static HeaderBrandingSettings Current { get; private set; } = Load();

    public static HeaderBrandingSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                Current = HeaderBrandingSettings.CreateDefault();
                return Current;
            }

            var json = File.ReadAllText(SettingsPath);
            Current = JsonSerializer.Deserialize<HeaderBrandingSettings>(json, JsonOptions)
                      ?? HeaderBrandingSettings.CreateDefault();
            return Current;
        }
        catch
        {
            Current = HeaderBrandingSettings.CreateDefault();
            return Current;
        }
    }

    public static void Save(HeaderBrandingSettings settings)
    {
        Current = settings;
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }
}
