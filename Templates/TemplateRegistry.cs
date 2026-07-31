namespace RonekaiImageFramer.Templates;

public static class TemplateRegistry
{
    private static readonly IProductTemplate[] All =
    [
        new NoTemplateTemplate(),
        new YayTemplate(),
        new WhiteStudioTemplate(),
        new BlackStudioTemplate(),
        new SoftShadowStudioTemplate(),
        new BrandBarBottomTemplate(),
        new BrandBarTopTemplate(),
        new MarketplaceSquareTemplate(),
        new InstagramSquareTemplate(),
        new InstagramPostPortraitBlackTemplate(),
        new InstagramPostPortraitWhiteTemplate(),
        new CatalogWideTemplate(),
        new DarkPremiumTemplate(),
        new MinimalFrameTemplate(),
        new DoubleLineFrameTemplate(),
        new SideBrandStripTemplate(),
        new CornerWatermarkTemplate(),
        new PinterestPinTemplate(),
        new StoryVerticalTemplate(),
        new PolaroidFrameTemplate(),
        new RoundedCardTemplate(),
        new TrendyolSquareTemplate(),
        new DiagonalAccentTemplate(),
        new LuxuryFrameTemplate(),
        new BannerStripTemplate(),
    ];

    public static IReadOnlyList<IProductTemplate> Templates => All;

    public static IProductTemplate? GetById(string id) =>
        All.FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}
