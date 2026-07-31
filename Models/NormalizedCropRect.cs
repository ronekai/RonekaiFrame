namespace RonekaiImageFramer.Models;

/// <summary>
/// Normalize edilmiş kırpma dikdörtgeni.
/// 0..1 aralığında Left/Top, Width/Height değerleriyle temsil edilir.
/// Uygulama sırasında çıktı görselinin piksel boyutlarına dönüştürülür.
/// </summary>
public sealed record NormalizedCropRect(double Left, double Top, double Width, double Height);

