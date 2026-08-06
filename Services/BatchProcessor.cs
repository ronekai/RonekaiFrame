using RonekaiImageFramer.Models;
using RonekaiImageFramer.Templates;
using SixLabors.ImageSharp.PixelFormats;
using System.Collections.Generic;

namespace RonekaiImageFramer.Services;

public sealed record ProcessProgress(int Current, int Total, string Message, bool IsError);

public sealed record ProcessResult(
    int Success,
    int Failed,
    int HeifInBatch,
    string OutputFolder,
    string? SamplePreviewFolder,
    IReadOnlyList<string> Log);

public static class BatchProcessor
{
    public static IReadOnlyList<string> FindImages(string sourceFolder) =>
        Directory.EnumerateFiles(sourceFolder, "*.*", SearchOption.TopDirectoryOnly)
            .Where(f => ImageInputCatalog.IsSupportedExtension(Path.GetExtension(f)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static int CountHeifImages(IEnumerable<string> files) =>
        files.Count(ImageInputCatalog.IsHeifFamilyFile);

    public static async Task<ProcessResult> ProcessFilesAsync(
        IReadOnlyList<string> files,
        string outputFolder,
        IProductTemplate? template,
        BrandColorTheme colorTheme,
        ThemeColorSet themeColors,
        LogoOverlaySettings logoSettings,
        ImageBrandSettings imageBrand,
        ExportResolutionProfile exportProfile,
        ProcessingJobSettings job,
        SourceFolderLogoSettings? folderLogoSettings = null,
        IProgress<ProcessProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (files.Count == 0)
            throw new InvalidOperationException("İşlenecek dosya yok.");

        Directory.CreateDirectory(outputFolder);

        int success = 0;
        int failed = 0;
        int heifInBatch = CountHeifImages(files);
        var log = new List<string>();
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string templateId = job.ResizeOnly ? "resize-only"
            : template?.Id ?? "none";
        string? sampleFolder = null;

        log.Add($"Islenecek: {files.Count} dosya, {heifInBatch} HEIC/HEIF/AVIF");
        log.Add($"Mod: {(job.ResizeOnly ? "Sadece boyutlandır"
            : template?.StretchToExport == true ? $"Yay → {exportProfile.Name}"
            : template?.IsPassthrough == true ? "Şablon yok"
            : $"Şablon: {template?.Name}")}");
        log.Add($"Çıktı: {exportProfile.Name} · {(job.SaveAsPng ? "PNG" : $"JPEG Q{job.JpegQuality}")}");

        if (job.SamplePreviewCount > 0)
        {
            sampleFolder = Path.Combine(outputFolder, "_Onizleme_Ornekleri");
            Directory.CreateDirectory(sampleFolder);
            var samples = files.Take(Math.Min(job.SamplePreviewCount, files.Count)).ToList();
            log.Add($"Örnek önizleme ({samples.Count} dosya): {sampleFolder}");
            var sampleResult = await ProcessFileListAsync(
                samples, sampleFolder, template, colorTheme, themeColors, logoSettings, imageBrand,
                exportProfile, job, stamp, templateId, folderLogoSettings, null, cancellationToken);
            success += sampleResult.Success;
            failed += sampleResult.Failed;
            foreach (var line in sampleResult.Messages)
                log.Add("  " + line);
        }

        var mainResult = await ProcessFileListAsync(
            files, outputFolder, template, colorTheme, themeColors, logoSettings, imageBrand,
            exportProfile, job, stamp, templateId, folderLogoSettings, progress, cancellationToken);

        success += mainResult.Success;
        failed += mainResult.Failed;
        log.AddRange(mainResult.Messages);

        return new ProcessResult(success, failed, heifInBatch, outputFolder, sampleFolder, log);
    }

    public static Task<ProcessResult> ProcessFolderAsync(
        string sourceFolder,
        IProductTemplate? template,
        string outputFolder,
        BrandColorTheme colorTheme,
        ThemeColorSet? themeColors = null,
        LogoOverlaySettings? logoSettings = null,
        ImageBrandSettings? imageBrand = null,
        ExportResolutionProfile? exportProfile = null,
        ProcessingJobSettings? job = null,
        IReadOnlyList<string>? onlyFiles = null,
        IProgress<ProcessProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(sourceFolder))
            throw new DirectoryNotFoundException($"Kaynak klasör bulunamadı: {sourceFolder}");

        var files = onlyFiles ?? FindImages(sourceFolder);
        if (files.Count == 0)
            throw new InvalidOperationException(
                $"Klasörde desteklenen görsel yok ({ImageInputCatalog.SupportedFormatsDescription}).");

        return ProcessFilesAsync(
            files,
            outputFolder,
            template,
            colorTheme,
            themeColors ?? ThemeColorSet.FromTheme(colorTheme),
            logoSettings ?? new LogoOverlaySettings(),
            imageBrand ?? ImageBrandStore.Current,
            exportProfile ?? ExportResolutionRegistry.Default,
            job ?? ProcessingJobSettings.Default,
            SourceFolderLogoStore.GetForFolder(sourceFolder),
            progress,
            cancellationToken);
    }

    private static async Task<(int Success, int Failed, List<string> Messages)> ProcessFileListAsync(
        IReadOnlyList<string> files,
        string outputFolder,
        IProductTemplate? template,
        BrandColorTheme colorTheme,
        ThemeColorSet themeColors,
        LogoOverlaySettings logoSettings,
        ImageBrandSettings imageBrand,
        ExportResolutionProfile exportProfile,
        ProcessingJobSettings job,
        string stamp,
        string templateId,
        SourceFolderLogoSettings? folderLogoSettings,
        IProgress<ProcessProgress>? progress,
        CancellationToken cancellationToken)
    {
        int success = 0;
        int failed = 0;
        var messages = new List<string>();
        var messagesLock = new object();
        int completed = 0;

        HashSet<string>? croppedOnlySelectedSet = null;
        if (job.CropOnlySelectedFiles && job.CropSelectedFilePaths.Count > 0)
        {
            croppedOnlySelectedSet = new HashSet<string>(job.CropSelectedFilePaths, StringComparer.OrdinalIgnoreCase);
        }

        int dop = Math.Clamp(Environment.ProcessorCount / 2, 1, 4);
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = dop,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(
            Enumerable.Range(0, files.Count),
            parallelOptions,
            async (i, ct) =>
            {
                var file = files[i];
                var fileName = Path.GetFileName(file);
                bool isHeif = ImageInputCatalog.IsHeifFile(file);

                try
                {
                    await Task.Run(() =>
                    {
                        string baseName = Path.GetFileNameWithoutExtension(file);
                        string outName = OutputFileNamer.BuildFileName(
                            job.FileNamePattern,
                            baseName,
                            stamp,
                            templateId,
                            colorTheme.Id,
                            exportProfile.Id,
                            logoSettings.ModeSuffix,
                            job.SaveAsPng);
                        string outPath = Path.Combine(outputFolder, outName);

                        var brandForFile = job.ProcessOnlySelectedFiles
                            ? BrandLogoResolver.ResolveForFile(
                                file,
                                imageBrand,
                                folderLogoSettings,
                                preferPerFileOverrides: true)
                            : imageBrand;

                        var cropOverride = croppedOnlySelectedSet is null
                            ? job.CropRect
                            : croppedOnlySelectedSet.Contains(file) ? job.CropRect : null;

                        ImagePipeline.ProcessAndSave(
                            file,
                            outPath,
                            template,
                            colorTheme,
                            themeColors,
                            logoSettings,
                            brandForFile,
                            exportProfile,
                            job,
                            cropOverride);
                    }, ct);

                    int done = Interlocked.Increment(ref completed);
                    Interlocked.Increment(ref success);
                    var okMsg = isHeif ? $"✓ {fileName} (HEIC)" : $"✓ {fileName}";
                    lock (messagesLock)
                        messages.Add(okMsg);
                    progress?.Report(new ProcessProgress(done, files.Count, okMsg, false));
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    int done = Interlocked.Increment(ref completed);
                    Interlocked.Increment(ref failed);
                    var errMsg = $"✗ {fileName}: {ex.Message}";
                    lock (messagesLock)
                        messages.Add(errMsg);
                    progress?.Report(new ProcessProgress(done, files.Count, errMsg, true));
                }
            });

        messages.Sort(StringComparer.OrdinalIgnoreCase);
        return (success, failed, messages);
    }
}

