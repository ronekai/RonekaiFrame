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
                ApplyFullBackground(result, logoWork, opacity, whiteWash: 0.2f);
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

    private static void ApplyFullBackground(Image<Rgba32> canvas, Image<Rgba32> logo, float opacity, float whiteWash)
    {
        using var bgLogo = ResizeCover(logo, canvas.Size);
        float logoAlpha = opacity * 0.72f;
        canvas.Mutate(ctx =>
        {
            ctx.DrawImage(bgLogo, ImgPoint.Empty, logoAlpha);
            if (whiteWash > 0)
            {
                var wash = BrandThemeColors.Background.ToPixel<Rgba32>();
                ctx.Fill(ImgColor.FromRgba(wash.R, wash.G, wash.B, (byte)(whiteWash * 140)));
            }
        });
    }

    private static void ApplyBackground(Image<Rgba32> canvas, Image<Rgba32> logo, float opacity, bool blur, float whiteWash)
    {
        using var bgLogo = ResizeCover(logo, canvas.Size);

        if (blur)
            bgLogo.Mutate(x => x.GaussianBlur(6));

        float logoAlpha = opacity * 0.45f;
        canvas.Mutate(ctx =>
        {
            ctx.DrawImage(bgLogo, ImgPoint.Empty, logoAlpha);
            if (whiteWash > 0)
            {
                var wash = BrandThemeColors.Background.ToPixel<Rgba32>();
                ctx.Fill(ImgColor.FromRgba(wash.R, wash.G, wash.B, (byte)(whiteWash * 160)));
            }
        });
    }

    private static void ApplyFrame(Image<Rgba32> canvas, Image<Rgba32> logo, float opacity)
    {
        int bandHeight = Math.Max(48, (int)(canvas.Height * 0.13));
        int topY = LogoPlacementContext.Top;
        int bottomY = canvas.Height - bandHeight - LogoPlacementContext.Bottom;

        using var topLogo = ResizeFitWidth(logo, canvas.Width, bandHeight);
        using var bottomLogo = topLogo.CloneAs<Rgba32>();

        canvas.Mutate(ctx =>
        {
            ctx.DrawImage(topLogo, new ImgPoint(0, topY), opacity);
            if (bottomY >= topY + bandHeight)
                ctx.DrawImage(bottomLogo, new ImgPoint(0, bottomY), opacity);
        });

        int sideBandH = canvas.Height - LogoPlacementContext.Top - LogoPlacementContext.Bottom;
        if (sideBandH < 48)
            return;

        using var sideLogo = ResizeFitHeight(logo, bandHeight, sideBandH);
        int sideMargin = Math.Max(8, (int)(canvas.Width * 0.02));
        int sideXLeft = sideMargin + LogoPlacementContext.Left;
        int sideXRight = canvas.Width - sideLogo.Width - sideMargin - LogoPlacementContext.Right;
        int sideY = LogoPlacementContext.Top;

        canvas.Mutate(ctx =>
        {
            ctx.DrawImage(sideLogo, new ImgPoint(sideXLeft, sideY), opacity * 0.85f);
            if (sideXRight > sideXLeft + sideLogo.Width)
                ctx.DrawImage(sideLogo, new ImgPoint(sideXRight, sideY), opacity * 0.85f);
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
            AnchorPositionMode.Right => canvas.Width - badge.Width - margin - LogoPlacementContext.Right,
            AnchorPositionMode.Center => (canvas.Width - badge.Width) / 2,
            _ => margin + LogoPlacementContext.Left
        };

        int y = vertical switch
        {
            AnchorPositionMode.Bottom => canvas.Height - badge.Height - margin - LogoPlacementContext.Bottom,
            AnchorPositionMode.Top => margin + LogoPlacementContext.Top,
            _ => (canvas.Height - badge.Height) / 2
        };

        x = Math.Clamp(x, LogoPlacementContext.Left, Math.Max(LogoPlacementContext.Left, canvas.Width - badge.Width - LogoPlacementContext.Right));
        y = Math.Clamp(y, LogoPlacementContext.Top, Math.Max(LogoPlacementContext.Top, canvas.Height - badge.Height - LogoPlacementContext.Bottom));

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
