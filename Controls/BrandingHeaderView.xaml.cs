using System.Windows;
using System.Windows.Controls;
using RonekaiImageFramer.Services;
using RonekaiImageFramer.Ui;

namespace RonekaiImageFramer.Controls;

public partial class BrandingHeaderView : UserControl
{
    public static readonly DependencyProperty ShowTaglineProperty =
        DependencyProperty.Register(
            nameof(ShowTagline),
            typeof(bool),
            typeof(BrandingHeaderView),
            new PropertyMetadata(true, (d, _) =>
            {
                if (d is BrandingHeaderView view)
                    view.RefreshBranding();
            }));

    /// <summary>false ise alt satır gizlenir (giriş ekranı).</summary>
    public bool ShowTagline
    {
        get => (bool)GetValue(ShowTaglineProperty);
        set => SetValue(ShowTaglineProperty, value);
    }

    public BrandingHeaderView()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshBranding();
    }

    public void RefreshBranding()
    {
        HeaderBrandingStore.Load();
        HeaderBrandingApplier.Apply(
            HeaderBrandingStore.Current,
            BrandLogoImage,
            BrandTitleRow,
            BrandMainText,
            BrandSuffixText,
            BrandTaglineText,
            ShowTagline);
    }
}
