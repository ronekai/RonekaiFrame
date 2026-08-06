namespace RonekaiImageFramer.Models;

/// <summary>
/// Şekil/seçim kopyasının kaynak görsele döndürülerek yapıştırılması.
/// PatchPng: RGBA PNG; DestCenter kaynak uzayında 0..1.
/// </summary>
public sealed record SelectionPasteOp(
    byte[] PatchPng,
    NormalizedPoint DestCenter,
    double RotationDegrees,
    TextureCloneBrushShape Shape = TextureCloneBrushShape.Square);
