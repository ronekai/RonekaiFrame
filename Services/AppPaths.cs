namespace RonekaiImageFramer.Services;

public static class AppPaths
{
    /// <summary>Programın çalıştığı kök klasör (exe veya proje dizini).</summary>
    public static string ProgramRoot => ResolveProgramRoot();

    /// <summary>Çıktı alt klasör adı (tarih damgalı).</summary>
    public static string BuildOutputFolderName(DateTime? at = null) =>
        $"PhonixFrame_{(at ?? DateTime.Now):yyyy-MM-dd_HHmmss}";

    /// <summary>
    /// Ana kaynak klasörün içinde tarih damgalı çıktı klasörü oluşturur.
    /// İşlenen kaynak klasör adıyla bir alt klasör açılır (ör. PhonixFrame_…/funko_illidan).
    /// </summary>
    public static string CreateOutputFolder(
        string rootSourceDirectory,
        string? activeSourceDirectory = null,
        bool nestSourceFolder = true)
    {
        if (string.IsNullOrWhiteSpace(rootSourceDirectory))
            throw new ArgumentException("Kaynak klasör belirtilmedi.", nameof(rootSourceDirectory));

        if (!Directory.Exists(rootSourceDirectory))
            throw new DirectoryNotFoundException($"Kaynak klasör bulunamadı: {rootSourceDirectory}");

        var outputRoot = Path.Combine(rootSourceDirectory, BuildOutputFolderName());
        if (!nestSourceFolder)
        {
            Directory.CreateDirectory(outputRoot);
            return outputRoot;
        }

        var path = ResolveOutputPath(outputRoot, rootSourceDirectory, activeSourceDirectory ?? rootSourceDirectory);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>Önizleme metni (klasör henüz oluşturulmadan).</summary>
    public static string PreviewOutputPath(string? rootSourceDirectory, string? activeSourceDirectory = null)
    {
        var folderName = BuildOutputFolderName();
        if (string.IsNullOrWhiteSpace(rootSourceDirectory) || !Directory.Exists(rootSourceDirectory))
            return $"(kaynak klasör)\\{folderName}\\(kaynak adı)";

        var outputRoot = Path.Combine(rootSourceDirectory, folderName);
        return ResolveOutputPath(outputRoot, rootSourceDirectory, activeSourceDirectory ?? rootSourceDirectory);
    }

    /// <summary>Toplu işlemde tarih damgalı çıktı kökü altındaki alt klasör adını döndürür.</summary>
    public static string ResolveRelativeOutputPath(string rootSourceDirectory, string sourceDirectory) =>
        ResolveSourceFolderLabel(rootSourceDirectory, sourceDirectory);

    private static string ResolveOutputPath(
        string outputRoot,
        string rootSourceDirectory,
        string activeSourceDirectory) =>
        Path.Combine(outputRoot, ResolveSourceFolderLabel(rootSourceDirectory, activeSourceDirectory));

    private static string ResolveSourceFolderLabel(string rootSourceDirectory, string activeSourceDirectory)
    {
        var relative = GetRelativeSubfolderPath(rootSourceDirectory, activeSourceDirectory);
        if (!string.IsNullOrEmpty(relative))
            return relative;

        var folderName = Path.GetFileName(
            Path.GetFullPath(activeSourceDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(folderName) ? "output" : folderName;
    }

    private static string? GetRelativeSubfolderPath(string rootSourceDirectory, string activeSourceDirectory)
    {
        var root = Path.GetFullPath(rootSourceDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var active = Path.GetFullPath(activeSourceDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (string.Equals(root, active, StringComparison.OrdinalIgnoreCase))
            return null;

        var relative = Path.GetRelativePath(root, active);
        if (relative.StartsWith("..", StringComparison.Ordinal))
            return null;

        return relative;
    }

    private static string ResolveProgramRoot()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath))
        {
            var processName = Path.GetFileName(processPath);
            if (!processName.Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
                return Path.GetDirectoryName(processPath)!;
        }

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (dir.GetFiles("*.csproj").Length > 0)
                return dir.FullName;
            dir = dir.Parent;
        }

        return AppContext.BaseDirectory;
    }
}
