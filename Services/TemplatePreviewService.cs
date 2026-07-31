using RonekaiImageFramer.Models;
using RonekaiImageFramer.Templates;
using RonekaiImageFramer.Ui;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ImgSize = SixLabors.ImageSharp.Size;

namespace RonekaiImageFramer.Services;

public sealed record LivePreviewResult(
    byte[]? PreviewPng,
    string SizeLabel,
    string Caption,
    bool Success,
    string? ErrorMessage,
    int OutputWidth = 0,
    int OutputHeight = 0);

public static class TemplatePreviewService
{
    private const int MaxDisplayWidth = 960;

    public static LivePreviewResult Render(
        IProductTemplate template,
        BrandColorTheme theme,
        ThemeColorSet themeColors,
        LogoOverlaySettings logoSettings,
        ImageBrandSettings imageBrand,
        ExportResolutionProfile exportProfile,
        ProcessingJobSettings job,
        string? sampleSourceFile = null,
        int? sampleSourceWidth = null,
        int? sampleSourceHeight = null)
    {
        try
        {
            using var _ = BrandThemeContext.Use(theme, themeColors);
            using var __ = ImageBrandContext.Use(imageBrand);
            using var ___ = ProcessingFitContext.Use(job.ResponsiveProductFit);

            // Filigram / klon çıktı uzayında; logo/marka bunlardan SONRA gelsin
            bool deferBrand = job.CropRect is not null
                              || job.WatermarkCleanOps.Count > 0
                              || job.TextureCloneOps.Count > 0;
            using var ____ = BrandOverlayDeferContext.Use(deferBrand);

            Image<Rgba32> sourceImage;
            bool usedRealPhoto = false;
            int srcW;
            int srcH;

            if (!string.IsNullOrEmpty(sampleSourceFile) && File.Exists(sampleSourceFile))
            {
                sourceImage = SourceImageLoader.Load(sampleSourceFile);
                usedRealPhoto = true;
                srcW = sourceImage.Width;
                srcH = sourceImage.Height;
            }
            else
            {
                sourceImage = DemoProductImage.Create();
                srcW = sampleSourceWidth ?? sourceImage.Width;
                srcH = sampleSourceHeight ?? sourceImage.Height;
            }

            try
            {
                bool skipFrame = job.ResizeOnly || template.IsPassthrough;
                bool stretchToExport = template.StretchToExport && !job.ResizeOnly;
                ImgSize templateSize = skipFrame ? new ImgSize(srcW, srcH) : template.OutputSize;

                Image<Rgba32> frame;
                if (skipFrame)
                {
                    frame = sourceImage.CloneAs<Rgba32>();
                    if (!deferBrand)
                    {
                        LogoPlacementContext.Reset();
                        ImageBrandOverlay.ApplyToCanvas(frame);
                    }
                }
                else
                {
                    LogoPlacementContext.Reset();
                    frame = template.Apply(sourceImage);
                }

                using (frame)
                {
                    Image<Rgba32> output;
                    if (!deferBrand)
                    {
                        using var withLogo = ApplyLogoIfNeeded(frame, logoSettings);
                        using var withText = job.TextOverlay.HasText
                            ? TextOverlayRenderer.Apply(withLogo, job.TextOverlay, theme)
                            : withLogo.CloneAs<Rgba32>();
                        output = OutputScaler.Apply(
                            withText,
                            exportProfile,
                            srcW,
                            srcH,
                            templateSize,
                            stretchToExport);
                    }
                    else
                    {
                        using var scaled = OutputScaler.Apply(
                            frame,
                            exportProfile,
                            srcW,
                            srcH,
                            templateSize,
                            stretchToExport);

                        if (job.CropRect is { } crop)
                            ImageCropper.ApplyNormalizedCrop(scaled, crop);

                        // Önce filigram, sonra klon, en sonda logo/marka
                        if (job.WatermarkCleanOps.Count > 0)
                            GeminiWatermarkCleaner.ApplyAll(scaled, job.WatermarkCleanOps);

                        if (job.TextureCloneOps.Count > 0)
                            TextureCloneService.ApplyAll(scaled, job.TextureCloneOps);

                        LogoPlacementContext.Reset();
                        ImageBrandOverlay.ApplyToCanvas(scaled);

                        using var withLogo = ApplyLogoIfNeeded(scaled, logoSettings);
                        output = job.TextOverlay.HasText
                            ? TextOverlayRenderer.Apply(withLogo, job.TextOverlay, theme)
                            : withLogo.CloneAs<Rgba32>();
                    }

                    using (output)
                    {
                        var png = WpfImageHelper.EncodePng(output, MaxDisplayWidth);

                        string sizeLabel = job.CropRect is not null
                            ? $"Çıktı: {output.Width} × {output.Height} px (kırp)"
                            : OutputScaler.FormatTargetLabel(exportProfile, templateSize, srcW, srcH, stretchToExport);

                        string logoNote = logoSettings.UsesLogo ? " · logo" : "";
                        string textNote = job.TextOverlay.HasText ? " · metin" : "";
                        string sourceNote = usedRealPhoto
                            ? $"Gerçek fotoğraf: {Path.GetFileName(sampleSourceFile)}"
                            : "Demo ürün görseli";
                        string brandNote = ImageBrandOverlay.ShouldApply ? " · marka" : "";
                        string modNote = job.ResizeOnly ? " · sadece boyutlandır"
                            : template.IsPassthrough && !template.StretchToExport ? " · şablon yok"
                            : template.StretchToExport ? " · yay"
                            : job.ResponsiveProductFit ? " · responsif"
                            : "";
                        modNote += brandNote;
                        if (job.CropRect is not null)
                            modNote += " · kırp";
                        if (job.WatermarkCleanOps.Count > 0)
                            modNote += $" · filigram×{job.WatermarkCleanOps.Count}";
                        if (job.TextureCloneOps.Count > 0)
                            modNote += $" · klon×{job.TextureCloneOps.Count}";

                        return new LivePreviewResult(
                            png,
                            sizeLabel,
                            $"{sourceNote} · {imageBrand.MainText}{imageBrand.SuffixText}{logoNote}{textNote}{modNote}",
                            true,
                            null,
                            output.Width,
                            output.Height);
                    }
                }
            }
            finally
            {
                if (usedRealPhoto)
                    sourceImage.Dispose();
            }
        }
        catch (Exception ex)
        {
            return new LivePreviewResult(null, "", "Önizleme oluşturulamadı", false, ex.Message);
        }
    }

    private static Image<Rgba32> ApplyLogoIfNeeded(Image<Rgba32> templated, LogoOverlaySettings logoSettings)
    {
        if (!logoSettings.UsesLogo)
            return templated.CloneAs<Rgba32>();

        try
        {
            using var loaded = LogoProvider.LoadDetails(logoSettings.LogoFilePath);
            using var logo = loaded.CloneImage();
            return LogoComposer.Apply(templated, logo, logoSettings);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Logo önizleme: {ex.Message}");
            return templated.CloneAs<Rgba32>();
        }
    }
}