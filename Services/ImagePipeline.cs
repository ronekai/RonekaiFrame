using RonekaiImageFramer.Models;
using RonekaiImageFramer.Templates;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
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
        ProcessingJobSettings job)
    {
        using var _ = BrandThemeContext.Use(colorTheme, themeColors);
        using var __ = ImageBrandContext.Use(imageBrand);
        using var ___ = ProcessingFitContext.Use(job.ResponsiveProductFit);
        using var input = SourceImageLoader.Load(sourceFile);

        ImgSize templateSize = template?.OutputSize ?? new ImgSize(input.Width, input.Height);
        Image<Rgba32> pipeline;

        bool skipFrame = job.ResizeOnly || template is null || template.IsPassthrough;
        bool stretchToExport = template?.StretchToExport == true && !job.ResizeOnly;

        if (skipFrame)
        {
            LogoPlacementContext.Reset();
            pipeline = input.CloneAs<Rgba32>();
            templateSize = new ImgSize(input.Width, input.Height);
        }
        else
        {
            LogoPlacementContext.Reset();
            using var templated = template!.Apply(input);
            pipeline = templated.CloneAs<Rgba32>();
        }

        Image<Rgba32>? logoApplied = null;
        try
        {
            if (logoSettings.UsesLogo)
            {
                using var loaded = LogoProvider.LoadDetails(logoSettings.LogoFilePath);
                using var logo = loaded.CloneImage();
                logoApplied = LogoComposer.Apply(pipeline, logo, logoSettings);
                pipeline.Dispose();
                pipeline = logoApplied;
                logoApplied = null;
            }

            if (job.TextOverlay.HasText)
            {
                var withText = TextOverlayRenderer.Apply(pipeline, job.TextOverlay, colorTheme);
                if (!ReferenceEquals(withText, pipeline))
                {
                    pipeline.Dispose();
                    pipeline = withText;
                }
            }

            using var scaled = OutputScaler.Apply(
                pipeline,
                exportProfile,
                input.Width,
                input.Height,
                templateSize,
                stretchToExport);

            if (job.SaveAsPng)
                scaled.SaveAsPng(outputPath, new PngEncoder { CompressionLevel = PngCompressionLevel.BestCompression });
            else
                scaled.SaveAsJpeg(outputPath, new JpegEncoder { Quality = Math.Clamp(job.JpegQuality, 50, 100) });
        }
        finally
        {
            pipeline.Dispose();
            logoApplied?.Dispose();
        }
    }
}
