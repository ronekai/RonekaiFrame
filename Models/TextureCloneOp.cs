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
/// Koordinatlar önizleme / şablon tuvali normalize (0..1).
/// ExactCopy + SourceRect: seçim alanını bire bir (sert kenar) hedefe kopyalar.
/// PatchPng: Kaynak al anında kilitlenen kesit (görünen önizleme pikselleri).
/// PatchBakeWidth/Height: kesitin alındığı önizleme tuvali (px) — damga ölçeklemesi için.
/// </summary>
public sealed record TextureCloneOp(
    NormalizedPoint SourceCenter,
    NormalizedPoint DestCenter,
    double RadiusNorm,
    TextureCloneBrushShape Shape = TextureCloneBrushShape.Circle,
    NormalizedCropRect? FillRect = null,
    double RotationDegrees = 0,
    bool ExactCopy = false,
    NormalizedCropRect? SourceRect = null,
    IReadOnlyList<NormalizedPoint>? SourcePolygon = null,
    byte[]? PatchPng = null,
    int PatchBakeWidth = 0,
    int PatchBakeHeight = 0);
