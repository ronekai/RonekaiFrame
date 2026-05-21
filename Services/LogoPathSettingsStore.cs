using System.IO;
using System.Text.Json;
using RonekaiImageFramer.Models;

namespace RonekaiImageFramer.Services;

public static class LogoPathSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static string SettingsPath =>
        Path.Combine(AppPaths.ProgramRoot, "logo-path-settings.json");

    public static LogoPathSettings Current { get; private set; } = Load();

    public static LogoPathSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                Current = LogoPathSettings.CreateDefault();
                return Current;
            }

            var json = File.ReadAllText(SettingsPath);
            Current = JsonSerializer.Deserialize<LogoPathSettings>(json, JsonOptions)
                      ?? LogoPathSettings.CreateDefault();
            return Current;
        }
        catch
        {
            Current = LogoPathSettings.CreateDefault();
            return Current;
        }
    }

    public static void Save(LogoPathSettings settings)
    {
        Current = new LogoPathSettings
        {
            UseDefaultLogo = settings.UseDefaultLogo,
            CustomLogoPath = settings.CustomLogoPath
        };
        var json = JsonSerializer.Serialize(Current, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }
}
