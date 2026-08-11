using RonekaiImageFramer.Models;
using RonekaiImageFramer.Templates;
using RonekaiImageFramer.Ui;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
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
    /// <summary>Önizleme çalışma / görüntüleme üst sınırı (uzun kenar). Küçültüp geri büyütmek bulanıklaştırır.</summary>
    private const int MaxPreviewWorkLongEdge = 1600;

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

            var cleanOps = job.ResolveWatermarkCleanOps(sampleSourceFile);
            var cloneOps = job.ResolveTextureCloneOps(sampleSourceFile);
            var pasteOps = job.ResolveSelectionPasteOps(sampleSourceFile);
            var cropRect = job.ResolveCropRect(sampleSourceFile);

            Image<Rgba32>? sourceImage = null;
            bool usedRealPhoto = false;
            int srcW;
            int srcH;

            if (!string.IsNullOrEmpty(sampleSourceFile) && File.Exists(sampleSourceFile))
            {
                sourceImage = PreviewSourceCache.GetClone(sampleSourceFile);
                usedRealPhoto = true;
                srcW = sourceImage.Width;
                srcH = sourceImage.Height;
            }
            else
            {
                sourceImage = DemoProductImage.Create();
                usedRealPhoto = false;
                srcW = sampleSourceWidth ?? sourceImage.Width;
                srcH = sampleSourceHeight ?? sourceImage.Height;
            }

            try
            {
                // Filigram/yapıştır kaynakta. Küçültünce geri büyütme YOK —
                // downscale→upscale tüm görüntüyü yumuşatır (klon sonrası bulanıklık).
                using var preparedSource = sourceImage.CloneAs<Rgba32>();
                sourceImage.Dispose();
                sourceImage = null;

                bool photoEdits = cleanOps.Count > 0 || pasteOps.Count > 0;
                if (photoEdits)
                    CapLongEdgeInPlace(preparedSource, MaxPreviewWorkLongEdge);

                if (cleanOps.Count > 0)
                    GeminiWatermarkCleaner.ApplyAll(preparedSource, cleanOps, previewFast: true);
                if (pasteOps.Count > 0)
                    SelectionPasteService.ApplyAll(preparedSource, pasteOps);

                // Dışını kırp → kaynakta, şablondan önce (şablon kırpılan alanı geri getirmesin)
                if (cropRect is not null)
                    ImageCropper.ApplyNormalizedCrop(preparedSource, cropRect);

                bool skipFrame = job.ResizeOnly || template.IsPassthrough;
                bool stretchToExport = template.StretchToExport && !job.ResizeOnly;
                bool extendEdges = job.ExtendTemplateEdges && !skipFrame;
                bool deferBrand = extendEdges;
                using var ____ = BrandOverlayDeferContext.Use(deferBrand);

                ImgSize templateSize = skipFrame
                    ? new ImgSize(preparedSource.Width, preparedSource.Height)
                    : template.ResolveOutputSize(preparedSource.Width, preparedSource.Height);

                Image<Rgba32> frame;
                if (skipFrame)
                {
                    frame = preparedSource.CloneAs<Rgba32>();
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
                    frame = template.Apply(preparedSource);
                    templateSize = new ImgSize(frame.Width, frame.Height);
                }

                if (extendEdges)
                    EdgePadFillService.Apply(frame, job.EdgePadSampleRect);

                // Önizleme 0..1 her zaman frame uzayı olsun (klon öncesi/sonrası kayma olmasın)
                CapLongEdgeInPlace(frame, MaxPreviewWorkLongEdge);
                templateSize = new ImgSize(frame.Width, frame.Height);

                if (cloneOps.Count > 0)
                    TextureCloneService.ApplyAll(frame, cloneOps);

                int scaleSrcW = frame.Width;
                int scaleSrcH = frame.Height;

                using (frame)
                {
                    Image<Rgba32> output;
                    int exportW;
                    int exportH;

                    if (deferBrand)
                    {
                        LogoPlacementContext.Reset();
                        ImageBrandOverlay.ApplyToCanvas(frame);
                    }

                    using var withLogo = ApplyLogoIfNeeded(frame, logoSettings);
                    using var withText = job.TextOverlay.HasText
                        ? TextOverlayRenderer.Apply(withLogo, job.TextOverlay, theme)
                        : withLogo.CloneAs<Rgba32>();
                    output = OutputScaler.Apply(
                        withText,
                        exportProfile,
                        scaleSrcW,
                        scaleSrcH,
                        templateSize,
                        stretchToExport);
                    exportW = output.Width;
                    exportH = output.Height;

                    using (output)
                    {
                        // Görüntüleme için küçült — logo zaten tam çıktı boyutunda yerleşti
                        var png = WpfImageHelper.EncodePng(output, MaxPreviewWorkLongEdge);

                        string sizeLabel = cropRect is not null
                            ? $"Çıktı: {exportW} × {exportH} px (kırp)"
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
                        if (cropRect is not null)
                            modNote += " · kırp";
                        if (cleanOps.Count > 0)
                            modNote += $" · filigram×{cleanOps.Count}";
                        if (cloneOps.Count > 0)
                            modNote += $" · klon×{cloneOps.Count}";
                        if (job.ExtendTemplateEdges && !skipFrame)
                            modNote += " · kenar uzat";

                        return new LivePreviewResult(
                            png,
                            sizeLabel,
                            $"{sourceNote} · {imageBrand.MainText}{imageBrand.SuffixText}{logoNote}{textNote}{modNote}",
                            true,
                            null,
                            exportW,
                            exportH);
                    }
                }
            }
            finally
            {
                sourceImage?.Dispose();
            }
        }
        catch (Exception ex)
        {
            return new LivePreviewResult(null, "", "Önizleme oluşturulamadı", false, ex.Message);
        }
    }

    private static bool CapLongEdgeInPlace(Image<Rgba32> image, int maxLongEdge)
    {
        int longEdge = Math.Max(image.Width, image.Height);
        if (longEdge <= maxLongEdge)
            return false;

        double scale = maxLongEdge / (double)longEdge;
        int w = Math.Max(1, (int)Math.Round(image.Width * scale));
        int h = Math.Max(1, (int)Math.Round(image.Height * scale));
        image.Mutate(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new ImgSize(w, h),
            Mode = ResizeMode.Stretch,
            Sampler = KnownResamplers.Lanczos3
        }));
        return true;
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
