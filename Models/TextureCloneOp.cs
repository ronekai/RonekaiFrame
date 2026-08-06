namespace RonekaiImageFramer.Models;

public enum TextureCloneBrushShape
{
    Circle = 0,
    Square = 1,
    SoftSquare = 2,
    Ellipse = 3,
    /// <summary>Varsayılan serbest seçim (klasik çerçeve).</summary>
    Normal = 4
}

/// <summary>
/// Klon damga: kaynak merkezden hedef merkeze yumuşak yama.
/// RadiusNorm: görsel kısa kenarına göre yarıçap (0..1).
/// FillRect doluysa seçim dikdörtgenine doku nakli yapar (RadiusNorm yok sayılır).
/// </summary>
public sealed record TextureCloneOp(
    NormalizedPoint SourceCenter,
    NormalizedPoint DestCenter,
    double RadiusNorm,
    TextureCloneBrushShape Shape = TextureCloneBrushShape.Circle,
    NormalizedCropRect? FillRect = null);
