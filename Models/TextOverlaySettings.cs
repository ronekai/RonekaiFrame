namespace RonekaiImageFramer.Models;

public enum TextOverlayPosition
{
    BottomCenter,
    BottomLeft,
    TopCenter
}

public sealed class TextOverlaySettings
{
    public bool Enabled { get; init; }
    public string Text { get; init; } = "";
    public TextOverlayPosition Position { get; init; } = TextOverlayPosition.BottomCenter;
    public float Opacity { get; init; } = 0.85f;

    public bool HasText => Enabled && !string.IsNullOrWhiteSpace(Text);
}
