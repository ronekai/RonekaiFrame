namespace RonekaiImageFramer.Models;

public sealed class PlacementListItem(OverlayPlacement placement, string name)
{
    public OverlayPlacement Placement { get; } = placement;
    public string Name { get; } = name;
}
