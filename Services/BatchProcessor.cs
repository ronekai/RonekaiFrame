using RonekaiImageFramer.Models;
using RonekaiImageFramer.Templates;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace RonekaiImageFramer.Services;

public sealed record ProcessProgress(int Current, int Total, string Message, bool IsError);

public sealed record ProcessResult(
    int Success,
    int Failed,
    int HeifInBatch,
    string OutputFolder,
    IReadOnlyList<string> Log);

public static class BatchProcessor
{
    public static IReadOnlyList<string> FindImages(string sourceFolder) =>
        Directory.EnumerateFiles(sourceFolder, "*.*", SearchOption.AllDirectories)
            .Where(f => ImageInputCatalog.IsSupportedExtension(Path.GetExtension(f)))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static int CountHeifImages(IEnumerable<string> files) =>
        files.Count(ImageInputCatalog.IsHeifFile);

    public static async Task<ProcessResult> ProcessFolderAsync(
        string sourceFolder,
        IProductTemplate template,
        string outputFolder,
        BrandColorTheme colorTheme,
        LogoOverlaySettings? logoSettings = null,
        ImageBrandSettings? imageBrand = null,
        ExportResolutionProfile? exportProfile = null,
        IProgress<ProcessProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(sourceFolder))
            throw new DirectoryNotFoundException($"Kaynak klasör bulunamadı: {sourceFolder}");

        var files = FindImages(sourceFolder);
        if (files.Count == 0)
            throw new InvalidOperationException(
                $"Klasörde desteklenen resim bulunamadı ({ImageInputCatalog.SupportedFormatsDescription}).\n" +
                "Alt klasörler de taranır; dosya uzantısının .heic / .jpg olduğundan emin olun.");

        Directory.CreateDirectory(outputFolder);

        int success = 0;
        int failed = 0;
        int heifInBatch = CountHeifImages(files);
        var log = new List<string>();
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        logoSettings ??= new LogoOverlaySettings();
        imageBrand ??= ImageBrandStore.Current;
        exportProfile ??= ExportResolutionRegistry.Default;
        using var logoLoaded = logoSettings.UsesLogo
            ? LogoProvider.LoadDetails(logoSettings.LogoFilePath)
            : null;
        using var logoImage = logoLoaded?.CloneImage();

        log.Add($"Toplam {files.Count} dosya (alt klasörler dahil), {heifInBatch} HEIC/HEIF");
        log.Add($"Çıktı boyutu: {exportProfile.Name} ({exportProfile.SizeHint})");
        log.Add($"Görsel marka: {imageBrand.MainText}{imageBrand.SuffixText}");

        for (int i = 0; i < files.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = files[i];
            var fileName = Path.GetFileName(file);
            bool isHeif = ImageInputCatalog.IsHeifFile(file);

            try
            {
                await Task.Run(() =>
                {
                    using var _ = BrandThemeContext.Use(colorTheme);
                    using var __ = ImageBrandContext.Use(imageBrand);
                    using var input = SourceImageLoader.Load(file);

                    using var templated = template.Apply(input);
                    Image<Rgba32> pipeline = templated;
                    Image<Rgba32>? logoApplied = null;

                    if (logoImage != null)
                    {
                        logoApplied = LogoComposer.Apply(templated, logoImage, logoSettings);
                        pipeline = logoApplied;
                    }

                    using var scaled = OutputScaler.Apply(
                        pipeline,
                        exportProfile,
                        input.Width,
                        input.Height,
                        template.OutputSize);

                    string baseName = Path.GetFileNameWithoutExtension(file);
                    string heifTag = isHeif ? "_heic" : "";
                    string outName = $"{baseName}_{stamp}_{template.Id}_{colorTheme.Id}_{exportProfile.Id}_{logoSettings.ModeSuffix}{heifTag}.jpg";
                    string outPath = Path.Combine(outputFolder, outName);
                    scaled.SaveAsJpeg(outPath, new JpegEncoder { Quality = 92 });

                    logoApplied?.Dispose();
                }, cancellationToken).ConfigureAwait(false);

                success++;
                var okMsg = isHeif ? $"✓ {fileName} (HEIC → JPEG)" : $"✓ {fileName}";
                log.Add(okMsg);
                progress?.Report(new ProcessProgress(i + 1, files.Count, okMsg, false));
            }
            catch (Exception ex)
            {
                failed++;
                var errMsg = $"✗ {fileName}: {ex.Message}";
                log.Add(errMsg);
                progress?.Report(new ProcessProgress(i + 1, files.Count, errMsg, true));
            }
        }

        return new ProcessResult(success, failed, heifInBatch, outputFolder, log);
    }
}
