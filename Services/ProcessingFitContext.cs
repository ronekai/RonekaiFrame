namespace RonekaiImageFramer.Services;

/// <summary>Şablon içinde ürün yerleşimi (contain vs cover).</summary>
public static class ProcessingFitContext
{
    private static readonly AsyncLocal<bool?> _responsive = new();

    public static bool ResponsiveProductFit => _responsive.Value ?? false;

    public static IDisposable Use(bool responsiveProductFit)
    {
        var previous = _responsive.Value;
        _responsive.Value = responsiveProductFit;
        return new Scope(previous);
    }

    private sealed class Scope(bool? previous) : IDisposable
    {
        public void Dispose() => _responsive.Value = previous;
    }
}
