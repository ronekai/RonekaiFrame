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

        var crop = job.ResolveCropRect(sourceFile, cropRectOverride);
        var cleanOps = job.ResolveWatermarkCleanOps(sourceFile);
        var cloneOps = job.ResolveTextureCloneOps(sourceFile);
        var pasteOps = job.ResolveSelectionPasteOps(sourceFile);

        using var input = SourceImageLoader.Load(sourceFile);
        using var prepared = PrepareSourceWithPhotoEdits(input, cleanOps, pasteOps);

        ImgSize templateSize = template?.ResolveOutputSize(prepared.Width, prepared.Height)
                               ?? new ImgSize(prepared.Width, prepared.Height);
        bool skipFrame = job.ResizeOnly || template is null || template.IsPassthrough;
        bool stretchToExport = template?.StretchToExport == true && !job.ResizeOnly;
        bool extendEdges = job.ExtendTemplateEdges && !skipFrame;
        // Marka/logo: kenar uzatma + klon sonrası çizilsin (uzatılan zeminin üstüne otursun)
        bool deferBrand = crop is not null || extendEdges;
        using var ____ = BrandOverlayDeferContext.Use(deferBrand);

        Image<Rgba32> frame;
        if (skipFrame)
        {
            frame = prepared.CloneAs<Rgba32>();
            templateSize = new ImgSize(prepared.Width, prepared.Height);
            ProductPlacementContext.SetIdentity(frame.Width, frame.Height);
            if (!deferBrand)
            {
                LogoPlacementContext.Reset();
                ImageBrandOverlay.ApplyToCanvas(frame);
            }
        }
        else
        {
            LogoPlacementContext.Reset();
            frame = template!.Apply(prepared);
            templateSize = new ImgSize(frame.Width, frame.Height);
        }

        if (extendEdges)
            EdgePadFillService.Apply(frame, job.EdgePadSampleRect);

        if (cloneOps.Count > 0)
            TextureCloneService.ApplyAll(frame, cloneOps);

        try
        {
            if (!deferBrand)
            {
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

            if (crop is null)
            {
                LogoPlacementContext.Reset();
                ImageBrandOverlay.ApplyToCanvas(frame);
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

            using var scaledForCrop = OutputScaler.Apply(
                frame,
                exportProfile,
                input.Width,
                input.Height,
                templateSize,
                stretchToExport);

            ImageCropper.ApplyNormalizedCrop(scaledForCrop, crop);

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

    /// <summary>Filigram + yapıştır kaynak fotoğrafta. Klon şablon tuvalinde uygulanır.</summary>
    public static Image<Rgba32> PrepareSourceWithPhotoEdits(
        Image<Rgba32> source,
        IReadOnlyList<WatermarkCleanOp> cleanOps,
        IReadOnlyList<SelectionPasteOp>? pasteOps = null)
    {
        var prepared = source.CloneAs<Rgba32>();
        if (cleanOps.Count > 0)
            GeminiWatermarkCleaner.ApplyAll(prepared, cleanOps);
        if (pasteOps is { Count: > 0 })
            SelectionPasteService.ApplyAll(prepared, pasteOps);
        return prepared;
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
            current.Dispose();
            current = withText;
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
                CompressionLevel = PngCompressionLevel.DefaultCompression
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
