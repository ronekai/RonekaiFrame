using System.IO;
using System.Text.Json;
using RonekaiImageFramer.Models;

namespace RonekaiImageFramer.Services;

public static class SourceFolderLogoStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly Dictionary<string, SourceFolderLogoSettings> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    private static string SettingsPath =>
        Path.Combine(AppPaths.ProgramRoot, "source-folder-logos.json");

    private sealed class RootDocument
    {
        public Dictionary<string, SourceFolderLogoSettings> Folders { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    public static SourceFolderLogoSettings GetForFolder(string? folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            return new SourceFolderLogoSettings();

        var key = BrandLogoResolver.NormalizePath(folderPath);
        if (Cache.TryGetValue(key, out var cached))
            return cached;

        LoadAll();
        if (Cache.TryGetValue(key, out cached))
            return cached;

        cached = new SourceFolderLogoSettings { FolderPath = key };
        Cache[key] = cached;
        return cached;
    }

    public static void Save(SourceFolderLogoSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.FolderPath))
            return;

        var key = BrandLogoResolver.NormalizePath(settings.FolderPath);
        settings.FolderPath = key;
        Cache[key] = settings.Clone();
        PersistAll();
    }

    private static void LoadAll()
    {
        Cache.Clear();
        try
        {
            if (!File.Exists(SettingsPath))
                return;

            var json = File.ReadAllText(SettingsPath);
            var doc = JsonSerializer.Deserialize<RootDocument>(json, JsonOptions);
            if (doc?.Folders is null)
                return;

            foreach (var kv in doc.Folders)
                Cache[kv.Key] = kv.Value;
        }
        catch
        {
            Cache.Clear();
        }
    }

    private static void PersistAll()
    {
        var doc = new RootDocument { Folders = Cache.ToDictionary(kv => kv.Key, kv => kv.Value) };
        var json = JsonSerializer.Serialize(doc, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }
}
