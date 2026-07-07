using RonekaiImageFramer.Models;
using SixLabors.ImageSharp;

namespace RonekaiImageFramer.Services;

public static class OverlayPlacementHelper
{
    public static Point GetTopLeft(
        OverlayPlacement placement,
        Size canvas,
        Size image,
        int margin = 24)
    {
        int x = placement switch
        {
            OverlayPlacement.TopRight or OverlayPlacement.MiddleRight or OverlayPlacement.BottomRight
                => canvas.Width - image.Width - margin,
            OverlayPlacement.TopCenter or OverlayPlacement.Center or OverlayPlacement.BottomCenter
                => (canvas.Width - image.Width) / 2,
            _ => margin
        };

        int y = placement switch
        {
            OverlayPlacement.BottomLeft or OverlayPlacement.BottomCenter or OverlayPlacement.BottomRight
                => canvas.Height - image.Height - margin,
            OverlayPlacement.MiddleLeft or OverlayPlacement.MiddleRight or OverlayPlacement.Center
                or OverlayPlacement.Diagonal
                => (canvas.Height - image.Height) / 2,
            _ => margin
        };

        return new Point(Math.Max(0, x), Math.Max(0, y));
    }

    public static HorizontalAlignment ToHorizontalAlignment(OverlayPlacement placement) =>
        placement switch
        {
            OverlayPlacement.TopRight or OverlayPlacement.MiddleRight or OverlayPlacement.BottomRight
                => HorizontalAlignment.Right,
            OverlayPlacement.TopCenter or OverlayPlacement.Center or OverlayPlacement.BottomCenter
                or OverlayPlacement.Diagonal
                => HorizontalAlignment.Center,
            _ => HorizontalAlignment.Left
        };
}
