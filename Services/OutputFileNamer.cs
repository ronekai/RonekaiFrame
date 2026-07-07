namespace RonekaiImageFramer.Services;

public static class OutputFileNamer
{
    public const string DefaultPattern = "{base}";

    public static string BuildFileName(
        string pattern,
        string baseName,
        string stamp,
        string templateId,
        string colorId,
        string exportId,
        string logoSuffix,
        bool saveAsPng)
    {
        string ext = saveAsPng ? ".png" : ".jpg";

        string name = (string.IsNullOrWhiteSpace(pattern) ? DefaultPattern : pattern)
            .Replace("{base}", Sanitize(baseName), StringComparison.OrdinalIgnoreCase)
            .Replace("{stamp}", stamp, StringComparison.OrdinalIgnoreCase)
            .Replace("{template}", templateId, StringComparison.OrdinalIgnoreCase)
            .Replace("{color}", colorId, StringComparison.OrdinalIgnoreCase)
            .Replace("{export}", exportId, StringComparison.OrdinalIgnoreCase)
            .Replace("{logo}", logoSuffix, StringComparison.OrdinalIgnoreCase)
            .Replace("{ext}", ext.TrimStart('.'), StringComparison.OrdinalIgnoreCase);

        if (!name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            name += ext;

        return SanitizeFileName(name);
    }

    private static string Sanitize(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');
        return value.Trim();
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
