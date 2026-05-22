using System.IO;
using System.Text.Json;
using RonekaiImageFramer.Models;

namespace RonekaiImageFramer.Services;

public static class ProcessingPresetStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static string SettingsPath =>
        Path.Combine(AppPaths.ProgramRoot, "processing-presets.json");

    public static List<ProcessingPreset> LoadAll()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return [new ProcessingPreset { Name = "Varsayılan" }];

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<List<ProcessingPreset>>(json, JsonOptions)
                   ?? [new ProcessingPreset()];
        }
        catch
        {
            return [new ProcessingPreset()];
        }
    }

    public static void SaveAll(IEnumerable<ProcessingPreset> presets)
    {
        var json = JsonSerializer.Serialize(presets.ToList(), JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }
}
