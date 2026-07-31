using RonekaiImageFramer.Models;
using RonekaiImageFramer.Templates;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ImgColor = SixLabors.ImageSharp.Color;
using ImgSize = SixLabors.ImageSharp.Size;

namespace RonekaiImageFramer.Services;

public static class ImagePipeline
{
    public static void ProcessAndSave(
        string sourceFile,
        string outputPath,
        IProductTemplate? template,
        BrandColorTheme colorTheme,
        ThemeColorSet themeColors,
        LogoOverlaySettings logoSettings,
        ImageBrandSettings imageBrand,
        ExportResolutionProfile exportProfile,
        ProcessingJobSettings job,
        NormalizedCropRect? cropRectOverride = null)
    {
        using var _ = BrandThemeContext.Use(colorTheme, themeColors);
        using var __ = ImageBrandContext.Use(imageBrand);
        using var ___ = ProcessingFitContext.Use(job.ResponsiveProductFit);

        var crop = cropRectOverride ?? job.CropRect;
        // Filigram / klon çıktı uzayında; logo/marka bunlardan SONRA gelsin
        bool deferBrand = crop is not null
                          || job.WatermarkCleanOps.Count > 0
                          || job.TextureCloneOps.Count > 0;
        using var ____ = BrandOverlayDeferContext.Use(deferBrand);

        using var input = SourceImageLoader.Load(sourceFile);

        ImgSize templateSize = template?.OutputSize ?? new ImgSize(input.Width, input.Height);
        bool skipFrame = job.ResizeOnly || template is null || template.IsPassthrough;
        bool stretchToExport = template?.StretchToExport == true && !job.ResizeOnly;

        Image<Rgba32> frame;
        if (skipFrame)
        {
            frame = input.CloneAs<Rgba32>();
            templateSize = new ImgSize(input.Width, input.Height);
            if (!deferBrand)
            {
                LogoPlacementContext.Reset();
                ImageBrandOverlay.ApplyToCanvas(frame);
            }
        }
        else
        {
            LogoPlacementContext.Reset();
            frame = template!.Apply(input);
        }

        try
        {
            if (!deferBrand)
            {
                // Logo/yazı ölçeklemeden ÖNCE: üst-alt çerçeve ve rozet rezervleri doğru çalışır
                using var withOverlays = ApplyLogoAndText(frame, logoSettings, job, colorTheme);
                using var scaled = OutputScaler.Apply(
                    withOverlays,
                    exportProfile,
                    input.Width,
                    input.Height,
                    templateSize,
                    stretchToExport);
                SaveToPath(scaled, outputPath, job, themeColors);
                return;
            }

            // Ölçekle → kırp → filigram → klon → marka + logo/yazı
            using var scaledForCrop = OutputScaler.Apply(
                frame,
                exportProfile,
                input.Width,
                input.Height,
                templateSize,
                stretchToExport);

            if (crop is not null)
                ImageCropper.ApplyNormalizedCrop(scaledForCrop, crop);

            if (job.WatermarkCleanOps.Count > 0)
                GeminiWatermarkCleaner.ApplyAll(scaledForCrop, job.WatermarkCleanOps);

            if (job.TextureCloneOps.Count > 0)
                TextureCloneService.ApplyAll(scaledForCrop, job.TextureCloneOps);

            LogoPlacementContext.Reset();
            ImageBrandOverlay.ApplyToCanvas(scaledForCrop);

            using var composed = ApplyLogoAndText(scaledForCrop, logoSettings, job, colorTheme);
            SaveToPath(composed, outputPath, job, themeColors);
        }
        finally
        {
            frame.Dispose();
        }
    }

    private static Image<Rgba32> ApplyLogoAndText(
        Image<Rgba32> canvas,
        LogoOverlaySettings logoSettings,
        ProcessingJobSettings job,
        BrandColorTheme colorTheme)
    {
        Image<Rgba32> current = canvas.CloneAs<Rgba32>();

        if (logoSettings.UsesLogo)
        {
            using var loaded = LogoProvider.LoadDetails(logoSettings.LogoFilePath);
            using var logo = loaded.CloneImage();
            var withLogo = LogoComposer.Apply(current, logo, logoSettings);
            current.Dispose();
            current = withLogo;
        }

        if (job.TextOverlay.HasText)
        {
            var withText = TextOverlayRenderer.Apply(current, job.TextOverlay, colorTheme);
            if (!ReferenceEquals(withText, current))
            {
                current.Dispose();
                current = withText;
            }
        }

        return current;
    }

    private static void SaveToPath(
        Image<Rgba32> image,
        string outputPath,
        ProcessingJobSettings job,
        ThemeColorSet themeColors)
    {
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        SaveOutput(image, outputPath, job, themeColors);
    }

    private static void SaveOutput(
        Image<Rgba32> image,
        string outputPath,
        ProcessingJobSettings job,
        ThemeColorSet themeColors)
    {
        if (job.SaveAsPng)
        {
            image.SaveAsPng(outputPath, new PngEncoder
            {
                CompressionLevel = PngCompressionLevel.BestCompression
            });
            return;
        }

        ImgColor flatten = ResolveFlattenColor(themeColors);
        image.Mutate(ctx => ctx.BackgroundColor(flatten));
        image.SaveAsJpeg(outputPath, new JpegEncoder
        {
            Quality = Math.Clamp(job.JpegQuality, 50, 100)
        });
    }

    private static ImgColor ResolveFlattenColor(ThemeColorSet themeColors)
    {
        try
        {
            var hex = themeColors.Background.PrimaryHex?.Trim();
            if (string.IsNullOrWhiteSpace(hex))
                return ImgColor.White;

            hex = hex.TrimStart('#');
            if (hex.Length == 3)
                hex = string.Concat(hex.Select(c => $"{c}{c}"));
            if (hex.Length != 6)
                return ImgColor.White;

            return ImgColor.ParseHex(hex);
        }
        catch
        {
            return ImgColor.White;
        }
    }
}