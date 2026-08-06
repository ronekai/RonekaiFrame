using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using RonekaiImageFramer.Models;
using RonekaiImageFramer.Services;
using RonekaiImageFramer.Ui;

namespace RonekaiImageFramer;

public partial class HeaderBrandingWindow : Window
{
    private readonly HeaderBrandingSettings _working;
    private static string AssetsFolder => LogoProvider.AssetsFolder;
    private static string HorizontalBlackPath => System.IO.Path.Combine(AssetsFolder, "nadir-figur-yatay-siyah.svg");
    private static string HorizontalWhitePath => System.IO.Path.Combine(AssetsFolder, "nadir-figur-yatay-beyaz.svg");

    public HeaderBrandingSettings? ResultSettings { get; private set; }

    public HeaderBrandingWindow(HeaderBrandingSettings current, Window owner)
    {
        InitializeComponent();
        Owner = owner;
        _working = Clone(current);

        ModeCombo.ItemsSource = new[]
        {
            new ModeItem(HeaderBrandingMode.Text, "Sadece metin"),
            new ModeItem(HeaderBrandingMode.Logo, "Sadece logo"),
            new ModeItem(HeaderBrandingMode.TextAndLogo, "Logo + metin"),
        };
        ModeCombo.DisplayMemberPath = nameof(ModeItem.Label);
        ModeCombo.SelectedValuePath = nameof(ModeItem.Mode);

        MainTextBox.Text = _working.MainText;
        SuffixTextBox.Text = _working.SuffixText;
        TaglineTextBox.Text = _working.Tagline;
        HeaderLogoPathBox.Text = _working.LogoPath ?? LogoProvider.DefaultLogoPath;

        ModeCombo.SelectedItem = ((ModeItem[])ModeCombo.ItemsSource)
            .First(i => i.Mode == _working.Mode);

        MainTextBox.TextChanged += (_, _) => RefreshPreview();
        SuffixTextBox.TextChanged += (_, _) => RefreshPreview();
        TaglineTextBox.TextChanged += (_, _) => RefreshPreview();

        RefreshPreview();
    }

    private void ModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshPreview();

    private void BrowseHeaderLogo_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Üst başlık logosu",
            Filter = LogoImageLoader.HeaderLogoDialogFilter,
            InitialDirectory = LogoProvider.AssetsFolder
        };

        if (dialog.ShowDialog() == true)
        {
            HeaderLogoPathBox.Text = dialog.FileName;
            RefreshPreview();
        }
    }

    private void HeaderLogoHorizontalBlack_Click(object sender, RoutedEventArgs e)
    {
        HeaderLogoPathBox.Text = HorizontalBlackPath;
        RefreshPreview();
    }

    private void HeaderLogoHorizontalWhite_Click(object sender, RoutedEventArgs e)
    {
        HeaderLogoPathBox.Text = HorizontalWhitePath;
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        if (ModeCombo.SelectedItem is ModeItem item)
            _working.Mode = item.Mode;

        _working.MainText = MainTextBox.Text.Trim();
        _working.SuffixText = SuffixTextBox.Text.Trim();
        _working.Tagline = TaglineTextBox.Text.Trim();
        _working.LogoPath = string.IsNullOrWhiteSpace(HeaderLogoPathBox.Text)
            ? null
            : HeaderLogoPathBox.Text.Trim();

        HeaderBrandingApplier.Apply(
            _working,
            PreviewLogo,
            PreviewTitleRow,
            PreviewMain,
            PreviewSuffix,
            PreviewTagline);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        RefreshPreview();
        ResultSettings = Clone(_working);
        HeaderBrandingStore.Save(ResultSettings);
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private static HeaderBrandingSettings Clone(HeaderBrandingSettings s) => new()
    {
        Mode = s.Mode,
        MainText = s.MainText,
        SuffixText = s.SuffixText,
        Tagline = s.Tagline,
        LogoPath = s.LogoPath
    };

    private sealed record ModeItem(HeaderBrandingMode Mode, string Label);
}
