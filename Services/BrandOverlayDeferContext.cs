namespace RonekaiImageFramer.Services;

/// <summary>
/// Kırpma sonrası marka/logo/yazı yeniden konumlanacaksa şablon aşamasında markayı erteletir.
/// </summary>
public static class BrandOverlayDeferContext
{
    private static readonly AsyncLocal<bool> Deferred = new();

    public static bool IsDeferred => Deferred.Value;

    public static IDisposable Use(bool deferOverlays)
    {
        var previous = Deferred.Value;
        Deferred.Value = deferOverlays;
        return new Restore(previous);
    }

    private sealed class Restore(bool previous) : IDisposable
    {
        public void Dispose() => Deferred.Value = previous;
    }
}
