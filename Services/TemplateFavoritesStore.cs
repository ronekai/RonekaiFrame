using System.IO;
using System.Text.Json;

namespace RonekaiImageFramer.Services;

public sealed class TemplateFavoritesData
{
    public List<string> FavoriteIds { get; set; } = [];
    public List<string> RecentIds { get; set; } = [];
}

public static class TemplateFavoritesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string PathFile =>
        Path.Combine(AppPaths.ProgramRoot, "template-favorites.json");

    public static TemplateFavoritesData Load() =>
        File.Exists(PathFile)
            ? JsonSerializer.Deserialize<TemplateFavoritesData>(File.ReadAllText(PathFile), JsonOptions) ?? new()
            : new TemplateFavoritesData();

    public static void Save(TemplateFavoritesData data) =>
        File.WriteAllText(PathFile, JsonSerializer.Serialize(data, JsonOptions));

    public static void ToggleFavorite(string templateId)
    {
        var data = Load();
        if (data.FavoriteIds.Contains(templateId, StringComparer.OrdinalIgnoreCase))
            data.FavoriteIds.RemoveAll(id => id.Equals(templateId, StringComparison.OrdinalIgnoreCase));
        else
            data.FavoriteIds.Add(templateId);
        Save(data);
    }

    public static void TouchRecent(string templateId)
    {
        var data = Load();
        data.RecentIds.RemoveAll(id => id.Equals(templateId, StringComparison.OrdinalIgnoreCase));
        data.RecentIds.Insert(0, templateId);
        if (data.RecentIds.Count > 8)
            data.RecentIds = data.RecentIds.Take(8).ToList();
        Save(data);
    }

    public static bool IsFavorite(string templateId) =>
        Load().FavoriteIds.Contains(templateId, StringComparer.OrdinalIgnoreCase);
}
