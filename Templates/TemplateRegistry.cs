namespace RonekaiImageFramer.Templates;

public static class TemplateRegistry
{
    private static readonly IProductTemplate[] All =
    [
        new WhiteStudioTemplate(),
        new SoftShadowStudioTemplate(),
        new BrandBarBottomTemplate(),
        new BrandBarTopTemplate(),
        new MarketplaceSquareTemplate(),
        new InstagramSquareTemplate(),
        new CatalogWideTemplate(),
        new DarkPremiumTemplate(),
        new MinimalFrameTemplate(),
        new DoubleLineFrameTemplate(),
        new SideBrandStripTemplate(),
        new CornerWatermarkTemplate(),
    ];

    public static IReadOnlyList<IProductTemplate> Templates => All;

    public static IProductTemplate? GetById(string id) =>
        All.FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}
