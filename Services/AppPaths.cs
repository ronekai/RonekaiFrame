namespace RonekaiImageFramer.Services;

public static class AppPaths
{
    /// <summary>Programın çalıştığı kök klasör (exe veya proje dizini).</summary>
    public static string ProgramRoot => ResolveProgramRoot();

    /// <summary>Çıktı alt klasör adı (tarih damgalı).</summary>
    public static string BuildOutputFolderName(DateTime? at = null) =>
        $"PhonixFrame_{(at ?? DateTime.Now):yyyy-MM-dd_HHmmss}";

    /// <summary>Seçili kaynak klasörün içinde yeni çıktı klasörü oluşturur.</summary>
    public static string CreateOutputFolder(string sourceDirectory)
    {
        if (string.IsNullOrWhiteSpace(sourceDirectory))
            throw new ArgumentException("Kaynak klasör belirtilmedi.", nameof(sourceDirectory));

        if (!Directory.Exists(sourceDirectory))
            throw new DirectoryNotFoundException($"Kaynak klasör bulunamadı: {sourceDirectory}");

        var path = Path.Combine(sourceDirectory, BuildOutputFolderName());
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>Önizleme metni (klasör henüz oluşturulmadan).</summary>
    public static string PreviewOutputPath(string? sourceDirectory)
    {
        var folderName = BuildOutputFolderName();
        if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
            return $"(kaynak klasör)\\{folderName}";

        return Path.Combine(sourceDirectory, folderName);
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
