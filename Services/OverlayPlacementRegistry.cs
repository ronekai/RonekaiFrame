using RonekaiImageFramer.Models;

namespace RonekaiImageFramer.Services;

public static class OverlayPlacementRegistry
{
    public static IReadOnlyList<PlacementListItem> All { get; } =
    [
        new(OverlayPlacement.Center, "Orta"),
        new(OverlayPlacement.TopLeft, "Sol üst"),
        new(OverlayPlacement.TopCenter, "Orta üst"),
        new(OverlayPlacement.TopRight, "Sağ üst"),
        new(OverlayPlacement.MiddleLeft, "Sol orta"),
        new(OverlayPlacement.MiddleRight, "Sağ orta"),
        new(OverlayPlacement.BottomLeft, "Sol alt"),
        new(OverlayPlacement.BottomCenter, "Orta alt"),
        new(OverlayPlacement.BottomRight, "Sağ alt"),
        new(OverlayPlacement.Diagonal, "Çapraz (ortadan)"),
    ];

    public static PlacementListItem GetByPlacement(OverlayPlacement placement) =>
        All.FirstOrDefault(p => p.Placement == placement) ?? All[0];

    public static OverlayPlacement Parse(string? id, OverlayPlacement fallback = OverlayPlacement.Center)
    {
        if (string.IsNullOrWhiteSpace(id))
            return fallback;
        return Enum.TryParse<OverlayPlacement>(id, true, out var parsed) ? parsed : fallback;
    }
}
