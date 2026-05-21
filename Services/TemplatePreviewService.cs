using System.Windows.Media.Imaging;
using RonekaiImageFramer.Models;
using SixLabors.ImageSharp;
using RonekaiImageFramer.Templates;
using RonekaiImageFramer.Ui;
using SixLabors.ImageSharp.PixelFormats;

namespace RonekaiImageFramer.Services;

public sealed record LivePreviewResult(
    BitmapSource? Image,
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
        LogoOverlaySettings logoSettings,
        ImageBrandSettings imageBrand,
        ExportResolutionProfile exportProfile,
        int? sampleSourceWidth = null,
        int? sampleSourceHeight = null)
    {
        try
        {
            using var _ = BrandThemeContext.Use(theme);
            using var __ = ImageBrandContext.Use(imageBrand);
            using var demo = DemoProductImage.Create();
            using var templated = template.Apply(demo);

            using var withLogo = ApplyLogoIfNeeded(templated, logoSettings);
            using var output = OutputScaler.Apply(
                withLogo,
                exportProfile,
                sampleSourceWidth ?? demo.Width,
                sampleSourceHeight ?? demo.Height,
                template.OutputSize);

            var bmp = WpfImageHelper.ToBitmapSource(output, MaxDisplayWidth);

            string logoNote = logoSettings.UsesLogo ? " · logo önizleme" : "";
            string brandNote = $"{imageBrand.MainText}{imageBrand.SuffixText}";
            return new LivePreviewResult(
                bmp,
                OutputScaler.FormatTargetLabel(exportProfile, template.OutputSize, sampleSourceWidth, sampleSourceHeight),
                $"Demo ürün · marka: {brandNote}{logoNote}",
                true,
                null);
        }
        catch (Exception ex)
        {
            return new LivePreviewResult(
                null,
                "",
                "Önizleme oluşturulamadı",
                false,
                ex.Message);
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
        catch
        {
            return templated.CloneAs<Rgba32>();
        }
    }
}
