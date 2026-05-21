using RonekaiImageFramer.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ImgSize = SixLabors.ImageSharp.Size;

namespace RonekaiImageFramer.Services;

public static class OutputScaler
{
    public static Image<Rgba32> Apply(
        Image<Rgba32> rendered,
        ExportResolutionProfile profile,
        int sourceWidth,
        int sourceHeight,
        ImgSize templateSize)
    {
        var target = ResolveTargetSize(
            profile, sourceWidth, sourceHeight, templateSize, rendered.Width, rendered.Height);
        if (target.Width == rendered.Width && target.Height == rendered.Height)
            return rendered.CloneAs<Rgba32>();

        var pad = BrandThemeContext.Current.Background;
        return rendered.Clone(ctx => ctx.Resize(new ResizeOptions
        {
            Size = target,
            Mode = ResizeMode.Pad,
            PadColor = pad,
            Sampler = KnownResamplers.Lanczos3,
            Position = AnchorPositionMode.Center
        }));
    }

    public static ImgSize ResolveTargetSize(
        ExportResolutionProfile profile,
        int sourceWidth,
        int sourceHeight,
        ImgSize templateSize,
        int renderedWidth,
        int renderedHeight)
    {
        return profile.Mode switch
        {
            ExportSizeMode.TemplateDefault => new ImgSize(renderedWidth, renderedHeight),
            ExportSizeMode.SourceNative => new ImgSize(
                Math.Max(1, sourceWidth),
                Math.Max(1, sourceHeight)),
            ExportSizeMode.Fixed => new ImgSize(
                Math.Max(1, profile.Width ?? templateSize.Width),
                Math.Max(1, profile.Height ?? templateSize.Height)),
            ExportSizeMode.MaxLongEdge => ScaleToMaxLongEdge(
                renderedWidth,
                renderedHeight,
                profile.MaxLongEdge ?? 1200),
            _ => new ImgSize(renderedWidth, renderedHeight)
        };
    }

    public static string FormatTargetLabel(
        ExportResolutionProfile profile,
        ImgSize templateSize,
        int? sampleSourceW = null,
        int? sampleSourceH = null)
    {
        if (profile.Mode == ExportSizeMode.SourceNative)
            return sampleSourceW > 0 && sampleSourceH > 0
                ? $"Çıktı: {sampleSourceW} × {sampleSourceH} px (kaynak boyutu)"
                : "Çıktı: her dosyanın kendi boyutu";

        var size = ResolveTargetSize(
            profile,
            sampleSourceW ?? templateSize.Width,
            sampleSourceH ?? templateSize.Height,
            templateSize,
            templateSize.Width,
            templateSize.Height);
        return $"Çıktı: {size.Width} × {size.Height} px";
    }

    private static ImgSize ScaleToMaxLongEdge(int width, int height, int maxEdge)
    {
        int longEdge = Math.Max(width, height);
        if (longEdge <= maxEdge)
            return new ImgSize(width, height);

        float scale = maxEdge / (float)longEdge;
        return new ImgSize(
            Math.Max(1, (int)(width * scale)),
            Math.Max(1, (int)(height * scale)));
    }
}
