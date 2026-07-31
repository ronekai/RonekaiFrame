namespace RonekaiImageFramer.Models;

/// <summary>
/// Klon damga: kaynak merkezden hedef merkeze yumuşak daire yama.
/// RadiusNorm: görsel kısa kenarına göre yarıçap (0..1).
/// </summary>
public sealed record TextureCloneOp(
    NormalizedPoint SourceCenter,
    NormalizedPoint DestCenter,
    double RadiusNorm);
