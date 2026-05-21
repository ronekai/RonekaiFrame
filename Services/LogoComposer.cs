using RonekaiImageFramer.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ImgColor = SixLabors.ImageSharp.Color;
using ImgSize = SixLabors.ImageSharp.Size;
using ImgPoint = SixLabors.ImageSharp.Point;

namespace RonekaiImageFramer.Services;

public static class LogoComposer
{
    public static Image<Rgba32> Apply(Image<Rgba32> canvas, Image<Rgba32> logo, LogoOverlaySettings settings)
    {
        if (settings.Mode == LogoOverlayMode.None)
            return canvas.CloneAs<Rgba32>();

        using var logoWork = logo.CloneAs<Rgba32>();
        var result = canvas.CloneAs<Rgba32>();
        float opacity = Math.Clamp(settings.Opacity, 0.05f, 1f);

        switch (settings.Mode)
        {
            case LogoOverlayMode.Filigran:
                DrawScaled(result, logoWork, opacity, scale: 0.62f, anchor: AnchorPositionMode.Center);
                break;
            case LogoOverlayMode.ArkaPlan:
                ApplyBackground(result, logoWork, opacity, blur: true, whiteWash: 0.55f);
                break;
            case LogoOverlayMode.TamArkaPlan:
                ApplyBackground(result, logoWork, opacity, blur: false, whiteWash: 0.25f);
                break;
            case LogoOverlayMode.Cerceve:
                ApplyFrame(result, logoWork, opacity);
                break;
            case LogoOverlayMode.RozetSagAlt:
                DrawBadge(result, logoWork, opacity, AnchorPositionMode.Right, AnchorPositionMode.Bottom);
                break;
            case LogoOverlayMode.RozetSolAlt:
                DrawBadge(result, logoWork, opacity, AnchorPositionMode.Left, AnchorPositionMode.Bottom);
                break;
            case LogoOverlayMode.MerkezRozet:
                DrawBadge(result, logoWork, opacity, AnchorPositionMode.Center, AnchorPositionMode.Bottom, scale: 0.28f);
                break;
        }

        return result;
    }

    private static void ApplyBackground(Image<Rgba32> canvas, Image<Rgba32> logo, float opacity, bool blur, float whiteWash)
    {
        using var foreground = canvas.CloneAs<Rgba32>();
        using var bgLogo = ResizeCover(logo, canvas.Size);

        if (blur)
            bgLogo.Mutate(x => x.GaussianBlur(6));

        canvas.Mutate(ctx =>
        {
            ctx.Fill(ImgColor.White);
            ctx.DrawImage(bgLogo, ImgPoint.Empty, opacity);
            if (whiteWash > 0)
                ctx.Fill(ImgColor.FromRgba(255, 255, 255, (byte)(whiteWash * 255)));
            ctx.DrawImage(foreground, ImgPoint.Empty, 1f);
        });
    }

    private static void ApplyFrame(Image<Rgba32> canvas, Image<Rgba32> logo, float opacity)
    {
        int bandHeight = Math.Max(48, (int)(canvas.Height * 0.13));

        using var topLogo = ResizeFitWidth(logo, canvas.Width, bandHeight);
        using var bottomLogo = topLogo.CloneAs<Rgba32>();

        canvas.Mutate(ctx =>
        {
            ctx.DrawImage(topLogo, new ImgPoint(0, 0), opacity);
            ctx.DrawImage(bottomLogo, new ImgPoint(0, canvas.Height - bandHeight), opacity);
        });

        using var sideLogo = ResizeFitHeight(logo, bandHeight, canvas.Height);
        int sideX = Math.Max(8, (int)(canvas.Width * 0.02));
        canvas.Mutate(ctx =>
        {
            ctx.DrawImage(sideLogo, new ImgPoint(sideX, 0), opacity * 0.85f);
            ctx.DrawImage(sideLogo, new ImgPoint(canvas.Width - sideLogo.Width - sideX, 0), opacity * 0.85f);
        });
    }

    private static void DrawBadge(
        Image<Rgba32> canvas,
        Image<Rgba32> logo,
        float opacity,
        AnchorPositionMode horizontal,
        AnchorPositionMode vertical,
        float scale = 0.22f)
    {
        int targetW = Math.Max(64, (int)(canvas.Width * scale));
        using var badge = ResizeFitWidth(logo, targetW, (int)(targetW * 1.05f));
        int margin = Math.Max(12, (int)(canvas.Width * 0.025));

        int x = horizontal switch
        {
            AnchorPositionMode.Right => canvas.Width - badge.Width - margin,
            AnchorPositionMode.Center => (canvas.Width - badge.Width) / 2,
            _ => margin
        };

        int y = vertical switch
        {
            AnchorPositionMode.Bottom => canvas.Height - badge.Height - margin,
            AnchorPositionMode.Top => margin,
            _ => (canvas.Height - badge.Height) / 2
        };

        canvas.Mutate(ctx =>
        {
            var pad = new RectangleF(x - 6, y - 6, badge.Width + 12, badge.Height + 12);
            ctx.Fill(ImgColor.FromRgba(255, 255, 255, 200), pad);
            ctx.DrawImage(badge, new ImgPoint(x, y), opacity);
        });
    }

    private static void DrawScaled(
        Image<Rgba32> canvas,
        Image<Rgba32> logo,
        float opacity,
        float scale,
        AnchorPositionMode anchor)
    {
        int w = Math.Max(32, (int)(canvas.Width * scale));
        int h = Math.Max(32, (int)(canvas.Height * scale));
        using var resized = logo.Clone(x => x.Resize(new ResizeOptions
        {
            Size = new ImgSize(w, h),
            Mode = ResizeMode.Max,
            Sampler = KnownResamplers.Lanczos3
        }));

        var point = GetAnchorPoint(canvas.Size, resized.Size, anchor);
        canvas.Mutate(ctx => ctx.DrawImage(resized, point, opacity));
    }

    private static ImgPoint GetAnchorPoint(ImgSize canvas, ImgSize image, AnchorPositionMode anchor)
    {
        int x = anchor switch
        {
            AnchorPositionMode.Right => canvas.Width - image.Width,
            AnchorPositionMode.Center => (canvas.Width - image.Width) / 2,
            _ => 0
        };
        int y = anchor switch
        {
            AnchorPositionMode.Bottom => canvas.Height - image.Height,
            AnchorPositionMode.Center => (canvas.Height - image.Height) / 2,
            _ => 0
        };
        return new ImgPoint(Math.Max(0, x), Math.Max(0, y));
    }

    private static Image<Rgba32> ResizeCover(Image<Rgba32> source, ImgSize target) =>
        source.Clone(x => x.Resize(new ResizeOptions
        {
            Size = target,
            Mode = ResizeMode.Crop,
            Position = AnchorPositionMode.Center,
            Sampler = KnownResamplers.Lanczos3
        }));

    private static Image<Rgba32> ResizeFitWidth(Image<Rgba32> source, int width, int maxHeight)
    {
        float ratio = width / (float)source.Width;
        int h = Math.Min(maxHeight, Math.Max(1, (int)(source.Height * ratio)));
        return source.Clone(x => x.Resize(width, h));
    }

    private static Image<Rgba32> ResizeFitHeight(Image<Rgba32> source, int maxWidth, int height)
    {
        float ratio = height / (float)source.Height;
        int w = Math.Min(maxWidth, Math.Max(1, (int)(source.Width * ratio)));
        return source.Clone(x => x.Resize(w, height));
    }
}
