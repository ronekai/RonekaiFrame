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
        // Üst kenar daha sıkı (daha az aşağı kayma), alt kenar taşmasın
        int topMargin = Math.Max(0, margin / 2);
        int edgeMargin = margin;

        int x = placement switch
        {
            OverlayPlacement.TopRight or OverlayPlacement.MiddleRight or OverlayPlacement.BottomRight
                => canvas.Width - image.Width - edgeMargin,
            OverlayPlacement.TopCenter or OverlayPlacement.Center or OverlayPlacement.BottomCenter
                => (canvas.Width - image.Width) / 2,
            _ => edgeMargin
        };

        int y = placement switch
        {
            OverlayPlacement.BottomLeft or OverlayPlacement.BottomCenter or OverlayPlacement.BottomRight
                => canvas.Height - image.Height - edgeMargin,
            OverlayPlacement.MiddleLeft or OverlayPlacement.MiddleRight or OverlayPlacement.Center
                or OverlayPlacement.Diagonal
                => (canvas.Height - image.Height) / 2,
            OverlayPlacement.TopLeft or OverlayPlacement.TopCenter or OverlayPlacement.TopRight
                => topMargin,
            _ => edgeMargin
        };

        int maxX = Math.Max(0, canvas.Width - image.Width);
        int maxY = Math.Max(0, canvas.Height - image.Height);
        return new Point(Math.Clamp(x, 0, maxX), Math.Clamp(y, 0, maxY));
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