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
    string? ErrorMessage);

public static class TemplatePreviewService
{
    private const int MaxDisplayWidth = 560;

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

                Image<Rgba32> pipeline;
                if (skipFrame)
                {
                    LogoPlacementContext.Reset();
                    pipeline = sourceImage.CloneAs<Rgba32>();
                }
                else
                {
                    LogoPlacementContext.Reset();
                    using var templated = template.Apply(sourceImage);
                    pipeline = templated.CloneAs<Rgba32>();
                }

                using (pipeline)
                {
                    using var withLogo = ApplyLogoIfNeeded(pipeline, logoSettings);
                    using var withText = job.TextOverlay.HasText
                        ? TextOverlayRenderer.Apply(withLogo, job.TextOverlay, theme)
                        : withLogo.CloneAs<Rgba32>();

                    using var output = OutputScaler.Apply(
                        withText,
                        exportProfile,
                        srcW,
                        srcH,
                        templateSize,
                        stretchToExport);

                    var png = WpfImageHelper.EncodePng(output, MaxDisplayWidth);

                    string logoNote = logoSettings.UsesLogo ? " · logo" : "";
                    string textNote = job.TextOverlay.HasText ? " · metin" : "";
                    string sourceNote = usedRealPhoto
                        ? $"Gerçek fotoğraf: {Path.GetFileName(sampleSourceFile)}"
                        : "Demo ürün görseli";
                    string modNote = job.ResizeOnly ? " · sadece boyutlandır"
                        : template.IsPassthrough && !template.StretchToExport ? " · şablon yok"
                        : template.StretchToExport ? " · yay"
                        : job.ResponsiveProductFit ? " · responsif"
                        : "";

                    return new LivePreviewResult(
                        png,
                        OutputScaler.FormatTargetLabel(exportProfile, templateSize, srcW, srcH, stretchToExport),
                        $"{sourceNote} · {imageBrand.MainText}{imageBrand.SuffixText}{logoNote}{textNote}{modNote}",
                        true,
                        null);
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
