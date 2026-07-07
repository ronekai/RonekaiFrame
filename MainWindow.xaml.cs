using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using RonekaiImageFramer.Models;
using RonekaiImageFramer.Services;
using RonekaiImageFramer.Templates;
using RonekaiImageFramer.Ui;

namespace RonekaiImageFramer;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<string> _log = [];
    private CancellationTokenSource? _cts;
    private string? _lastOutputFolder;
    private bool _updatingOpacity;
    private string _customBackgroundHex = "#F5F6F8";
    private string _customRonekaiHex = "#1B2A4A";
    private string _customDenHex = "#C9A227";
    private CancellationTokenSource? _previewCts;
    private bool _previewReady;
    private bool _updatingBrandFields;
    private bool _updatingLogoPathUi;
    private bool _updatingColorFields;
    private string? _customLogoPath;
    private bool _loadingPreset;
    private List<ProcessingPreset> _presets = [];
    private string? _eyedropperColorField;
    private ThemeColorSet _themeColors = ThemeColorSet.FromHex("#F5F6F8", "#1B2A4A", "#C9A227");
    private ThemeColorAppearance _brandLogoTint = ThemeColorAppearance.FromHex("#1B2A4A", "#C9A227");
    private bool _updatingAppearanceUi;
    private bool _updatingBrandLogoUi;
    private string? _brandLogoEditingFilePath;
    private string? _lastSourceFolderPath;
    private string? _lastActiveSourceFolder;
    private bool _updatingSourceFolderUi;

    private sealed record FolderListItem(string Label, string Path);

    private bool IsPerFileBrandLogoMode() => ProcessSelectedOnlyCheck?.IsChecked == true;

    private static bool IsFileInSourceFolder(string filePath, string folderPath)
    {
        var parent = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(parent))
            return false;
        return string.Equals(
            BrandLogoResolver.NormalizePath(parent),
            BrandLogoResolver.NormalizePath(folderPath),
            StringComparison.OrdinalIgnoreCase);
    }

    private static void PrunePerFileEntries(SourceFolderLogoSettings settings, string folderPath)
    {
        var allowed = new HashSet<string>(
            BatchProcessor.FindImages(folderPath).Select(BrandLogoResolver.NormalizePath),
            StringComparer.OrdinalIgnoreCase);
        foreach (var key in settings.PerFile.Keys.Where(k => !allowed.Contains(k)).ToList())
            settings.PerFile.Remove(key);
    }

    public MainWindow()
    {
        _updatingAppearanceUi = true;
        _updatingBrandFields = true;
        InitializeComponent();
        LogoOpacitySlider.ValueChanged += LogoOpacitySlider_ValueChanged;
        BrandLogoOffsetXSlider.ValueChanged += BrandLogoSlider_Changed;
        BrandLogoOffsetYSlider.ValueChanged += BrandLogoSlider_Changed;
        BrandLogoSizeSlider.ValueChanged += BrandLogoSlider_Changed;
        BrandLogoOpacitySlider.ValueChanged += BrandLogoSlider_Changed;
        BrandLogoTintOpacitySlider.ValueChanged += BrandLogoTintAppearance_Changed;
        LogList.ItemsSource = _log;

        LogoPlacementCombo.ItemsSource = OverlayPlacementRegistry.All;
        BrandLogoPlacementCombo.ItemsSource = OverlayPlacementRegistry.All;
        BrandLogoCatalog.EnsureBundledLogos();
        InitializeBrandLogoTintCombos();
        InitializeImageBrandFields();
        InitializeBrandLogoFields();
        InitializeBrandFontCombos();
        _updatingBrandFields = false;

        RefreshTemplateComboItems(selectId: "white-studio");
        OutputFileNameBox.Text = OutputFileNamer.DefaultPattern;
        OutputFileNameBox.ToolTip =
            "Varsayılan: {base} — orijinal dosya adı (tarih zaten çıktı klasöründe). " +
            "İsteğe bağlı: stamp, template, color, export, logo, ext";
        TextOverlayPositionCombo.ItemsSource = new[]
        {
            new ComboTextItem("Alt orta", TextOverlayPosition.BottomCenter),
            new ComboTextItem("Alt sol", TextOverlayPosition.BottomLeft),
            new ComboTextItem("Üst orta", TextOverlayPosition.TopCenter)
        };
        TextOverlayPositionCombo.DisplayMemberPath = "Label";
        TextOverlayPositionCombo.SelectedIndex = 0;
        TextOverlayEnabledCheck.Checked += (_, _) => TextOverlayTextBox.IsEnabled = TextOverlayEnabledCheck.IsChecked == true;
        TextOverlayEnabledCheck.Unchecked += (_, _) => TextOverlayTextBox.IsEnabled = TextOverlayEnabledCheck.IsChecked == true;
        JpegQualitySlider.ValueChanged += JpegQualitySlider_ValueChanged;
        LoadPresets();

        RefreshExportResolutionComboItems();
        RefreshExportResolutionHint();

        ColorPackCombo.ItemsSource = ColorPackRegistry.All;
        ColorPackCombo.SelectedIndex = 0;
        InitializeColorAppearanceUi();
        RefreshColorPackUi();

        LogoModeCombo.ItemsSource = LogoModeRegistry.All;
        LogoModeCombo.SelectedIndex = 0;
        if (LogoPlacementCombo.SelectedItem is null)
            LogoPlacementCombo.SelectedItem = OverlayPlacementRegistry.GetByPlacement(OverlayPlacement.Center);
        RefreshLogoModeUi();

        Directory.CreateDirectory(LogoProvider.AssetsFolder);
        UseDefaultLogoCheck.Checked += UseDefaultLogoCheck_Changed;
        UseDefaultLogoCheck.Unchecked += UseDefaultLogoCheck_Changed;
        InitializeLogoPath();
        UpdateOutputPreview();
        BrandingSettings.SettingsRequested += (_, _) => OpenBrandingSettings();
        BrandingHeader.RefreshBranding();
        RefreshImageCount();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _previewReady = true;
        _lastSourceFolderPath = GetRootSourceFolder();
        _lastActiveSourceFolder = GetActiveSourceFolder();
        RefreshSubfolderCombo(_lastSourceFolderPath);
        RefreshColorFieldLabels();
        RefreshProcessSelectionHint();
        RefreshBrandLogoScopeHint();
        Dispatcher.BeginInvoke(ScheduleLivePreview, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void InitializeImageBrandFields()
    {
        BrandLogoCatalog.EnsureBundledLogos();
        var brand = ImageBrandStore.Load();
        _updatingBrandFields = true;
        ImageBrandMainBox.Text = brand.MainText;
        ImageBrandSuffixBox.Text = brand.SuffixText;
        ShowImageBrandMainCheck.IsChecked = brand.ShowMainText;
        ShowImageBrandSuffixCheck.IsChecked = brand.ShowSuffixText;
        ImageBrandMainSizeSlider.Value = brand.MainTextSizePercent;
        ImageBrandSuffixSizeSlider.Value = brand.SuffixTextSizePercent;
        _updatingBrandFields = false;
        RefreshImageBrandSizeUi();
        RefreshColorFieldLabels();
    }

    private void InitializeBrandLogoFields()
    {
        var brand = ImageBrandStore.Load();
        _updatingBrandLogoUi = true;
        if (string.IsNullOrWhiteSpace(brand.BrandLogoPath) && File.Exists(BrandLogoCatalog.WhiteLogoPath))
            brand.BrandLogoPath = BrandLogoCatalog.WhiteLogoPath;
        BindBrandLogoUiFromSettings(brand);
        _updatingBrandLogoUi = false;
        RefreshBrandLogoUi();
    }

    private void InitializeBrandFontCombos()
    {
        var fonts = BrandFontRegistry.All;
        BrandMainFontCombo.ItemsSource = fonts;
        BrandSuffixFontCombo.ItemsSource = fonts;
        var brand = ImageBrandStore.Load();
        SelectFontCombo(BrandMainFontCombo, brand.MainFontId);
        SelectFontCombo(BrandSuffixFontCombo, brand.SuffixFontId);
    }

    private static void SelectFontCombo(ComboBox combo, string? fontId)
    {
        var match = BrandFontRegistry.GetById(fontId) ?? BrandFontRegistry.Default;
        foreach (BrandFontOption item in combo.Items)
        {
            if (item.Id.Equals(match.Id, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = item;
                return;
            }
        }

        if (combo.Items.Count > 0)
            combo.SelectedIndex = 0;
    }

    private ImageBrandSettings BuildImageBrandSettings()
    {
        if (!IsImageBrandUiReady())
            return ImageBrandStore.Current.Clone();

        return new ImageBrandSettings
        {
            MainText = string.IsNullOrWhiteSpace(ImageBrandMainBox.Text)
                ? "RONEKAI"
                : ImageBrandMainBox.Text.Trim(),
            SuffixText = ImageBrandSuffixBox.Text?.Trim() ?? "",
            MainFontId = BrandMainFontCombo.SelectedItem is BrandFontOption main
                ? main.Id
                : BrandFontRegistry.Default.Id,
            SuffixFontId = BrandSuffixFontCombo.SelectedItem is BrandFontOption suffix
                ? suffix.Id
                : BrandFontRegistry.Default.Id,
            ShowMainText = ShowImageBrandMainCheck.IsChecked == true,
            ShowSuffixText = ShowImageBrandSuffixCheck.IsChecked == true,
            MainTextSizePercent = (int)ImageBrandMainSizeSlider.Value,
            SuffixTextSizePercent = (int)ImageBrandSuffixSizeSlider.Value,
            ShowBrandLogo = ShowBrandLogoCheck.IsChecked == true,
            BrandLogoPresetId = BrandLogoCatalog.DetectPresetId(
                string.IsNullOrWhiteSpace(BrandLogoPathBox.Text) ? null : BrandLogoPathBox.Text.Trim()),
            BrandLogoPath = string.IsNullOrWhiteSpace(BrandLogoPathBox.Text)
                ? null
                : BrandLogoPathBox.Text.Trim(),
            BrandLogoSizePercent = (int)BrandLogoSizeSlider.Value,
            BrandLogoOpacity = (float)(BrandLogoOpacitySlider.Value / 100.0),
            BrandLogoPlacement = BrandLogoPlacementCombo.SelectedItem is PlacementListItem p
                ? p.Placement
                : OverlayPlacement.BottomRight,
            BrandLogoOffsetX = (int)BrandLogoOffsetXSlider.Value,
            BrandLogoOffsetY = (int)BrandLogoOffsetYSlider.Value,
            BrandLogoTintEnabled = BrandLogoTintEnabledCheck.IsChecked == true,
            BrandLogoTint = ReadBrandLogoTintFromUi().Clone()
        };
    }

    private ThemeColorAppearance ReadBrandLogoTintFromUi()
    {
        ReadBrandLogoTintAppearanceFromUi();
        return _brandLogoTint.Clone();
    }

    private void ReadBrandLogoTintAppearanceFromUi()
    {
        if (_updatingBrandFields || BrandLogoTintFillModeCombo is null)
            return;

        if (BrandLogoTintFillModeCombo.SelectedValue is ColorFillMode mode)
            _brandLogoTint.FillMode = mode;
        if (BrandLogoTintOpacitySlider is not null)
            _brandLogoTint.Opacity = (float)(BrandLogoTintOpacitySlider.Value / 100.0);
        if (BrandLogoTintGradientEndHexBox is not null
            && UiColorHelper.TryParseColorInput(BrandLogoTintGradientEndHexBox.Text, out var end))
            _brandLogoTint.GradientEndHex = end;
        if (BrandLogoTintGradientDirectionCombo?.SelectedValue is GradientDirection dir)
            _brandLogoTint.GradientDirection = dir;
        if (BrandLogoTintHexBox is not null
            && UiColorHelper.TryParseColorInput(BrandLogoTintHexBox.Text, out var primary))
            _brandLogoTint.PrimaryHex = primary;
    }

    private bool IsImageBrandUiReady() =>
        ImageBrandMainBox is not null
        && ImageBrandSuffixBox is not null
        && BrandMainFontCombo is not null
        && BrandSuffixFontCombo is not null
        && ShowImageBrandMainCheck is not null
        && ShowImageBrandSuffixCheck is not null
        && ImageBrandMainSizeSlider is not null
        && ImageBrandSuffixSizeSlider is not null
        && ShowBrandLogoCheck is not null
        && BrandLogoPathBox is not null
        && BrandLogoPlacementCombo is not null
        && BrandLogoSizeSlider is not null
        && BrandLogoOpacitySlider is not null
        && BrandLogoOffsetXSlider is not null
        && BrandLogoOffsetYSlider is not null
        && BrandLogoTintEnabledCheck is not null
        && BrandLogoTintFillModeCombo is not null;

    private void ImageBrandVisibility_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingBrandFields || _loadingPreset)
            return;
        RefreshImageBrandSizeUi();
        PersistImageBrandSettings();
        ScheduleLivePreview();
    }

    private void ImageBrandSize_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingBrandFields || _loadingPreset || !_previewReady)
            return;
        if (ImageBrandMainSizeLabel is null || ImageBrandSuffixSizeLabel is null)
            return;
        ImageBrandMainSizeLabel.Text = $"{(int)ImageBrandMainSizeSlider.Value}%";
        ImageBrandSuffixSizeLabel.Text = $"{(int)ImageBrandSuffixSizeSlider.Value}%";
        PersistImageBrandSettings();
        ScheduleLivePreview();
    }

    private void InitializeBrandLogoTintCombos()
    {
        var fillItems = new object[]
        {
            new AppearanceComboItem("Düz renk", ColorFillMode.Solid),
            new AppearanceComboItem("Gradyan", ColorFillMode.Gradient)
        };
        var dirItems = new object[]
        {
            new AppearanceComboItem("Dikey", GradientDirection.Vertical),
            new AppearanceComboItem("Yatay", GradientDirection.Horizontal),
            new AppearanceComboItem("Çapraz ↘", GradientDirection.DiagonalDown),
            new AppearanceComboItem("Çapraz ↗", GradientDirection.DiagonalUp)
        };

        if (BrandLogoTintFillModeCombo is not null)
        {
            BrandLogoTintFillModeCombo.ItemsSource = fillItems;
            BrandLogoTintFillModeCombo.DisplayMemberPath = "Label";
            BrandLogoTintFillModeCombo.SelectedValuePath = "Value";
        }

        if (BrandLogoTintGradientDirectionCombo is not null)
        {
            BrandLogoTintGradientDirectionCombo.ItemsSource = dirItems;
            BrandLogoTintGradientDirectionCombo.DisplayMemberPath = "Label";
            BrandLogoTintGradientDirectionCombo.SelectedValuePath = "Value";
        }
    }

    private void BindBrandLogoTintUi()
    {
        if (BrandLogoTintFillModeCombo is null)
            return;

        _updatingBrandFields = true;
        BindSlotAppearanceUi(
            BrandLogoTintFillModeCombo,
            BrandLogoTintOpacitySlider,
            BrandLogoTintOpacityLabel,
            BrandLogoTintGradientPanel,
            BrandLogoTintGradientEndHexBox,
            BrandLogoTintGradientDirectionCombo,
            _brandLogoTint);
        if (BrandLogoTintHexBox is not null)
            BrandLogoTintHexBox.Text = _brandLogoTint.PrimaryHex.ToUpperInvariant();
        if (BrandLogoTintPreview is not null)
            BrandLogoTintPreview.Background = AppearanceBrushHelper.ToPreviewBrush(_brandLogoTint);
        _updatingBrandFields = false;
    }

    private void BrandLogoTintAppearance_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingBrandLogoUi || !_previewReady)
            return;
        ReadBrandLogoTintAppearanceFromUi();
        BindBrandLogoTintUi();
        BrandLogoRenderer.ClearCache();
        PersistBrandLogoFromUi();
        ScheduleLivePreview();
    }

    private void BrandLogoTintHexBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox box)
            ApplyBrandLogoTintHexFromBox(box);
    }

    private void BrandLogoTintHexBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox box)
            ApplyBrandLogoTintHexFromBox(box);
    }

    private void ApplyBrandLogoTintHexFromBox(TextBox box)
    {
        if (!UiColorHelper.TryParseColorInput(box.Text, out var hex))
            return;

        if (ColorFieldTags.IsBrandLogoTintEnd(box.Tag as string))
            _brandLogoTint.GradientEndHex = hex;
        else
            _brandLogoTint.PrimaryHex = hex;

        box.Text = hex.ToUpperInvariant();
        BindBrandLogoTintUi();
        BrandLogoRenderer.ClearCache();
        PersistBrandLogoFromUi();
        ScheduleLivePreview();
    }

    private void PickBrandLogoTint_Click(object sender, RoutedEventArgs e)
    {
        CancelEyedropper();
        ReadBrandLogoTintAppearanceFromUi();

        var dialog = new ColorPickerWindow(_brandLogoTint.PrimaryHex, this)
        {
            Title = "Marka logo rengi — PhonixFrame"
        };

        if (dialog.ShowDialog() != true)
            return;

        _brandLogoTint.PrimaryHex = dialog.SelectedHex;
        if (_brandLogoTint.FillMode == ColorFillMode.Solid)
            _brandLogoTint.GradientEndHex = dialog.SelectedHex;
        BindBrandLogoTintUi();
        BrandLogoRenderer.ClearCache();
        PersistBrandLogoFromUi();
        ScheduleLivePreview();
    }

    private void RefreshImageBrandSizeUi()
    {
        if (ImageBrandMainSizeSlider is null || ImageBrandSuffixSizeSlider is null)
            return;

        bool showMain = ShowImageBrandMainCheck?.IsChecked == true;
        bool showSuffix = ShowImageBrandSuffixCheck?.IsChecked == true;
        ImageBrandMainSizeSlider.IsEnabled = showMain;
        ImageBrandSuffixSizeSlider.IsEnabled = showSuffix;

        if (ImageBrandMainSizeLabel is not null)
            ImageBrandMainSizeLabel.Text = $"{(int)ImageBrandMainSizeSlider.Value}%";
        if (ImageBrandSuffixSizeLabel is not null)
            ImageBrandSuffixSizeLabel.Text = $"{(int)ImageBrandSuffixSizeSlider.Value}%";
    }

    private void RefreshBrandLogoUi()
    {
        if (!IsBrandLogoUiReady())
            return;

        bool perFileMode = IsPerFileBrandLogoMode();
        bool canEdit = !perFileMode || GetActiveLogoEditFile() is not null;
        bool showLogo = ShowBrandLogoCheck.IsChecked == true;

        if (ApplyBrandLogoToAllButton is not null)
            ApplyBrandLogoToAllButton.Visibility = perFileMode ? Visibility.Collapsed : Visibility.Visible;

        ShowBrandLogoCheck.IsEnabled = canEdit;
        BrowseBrandLogoButton.IsEnabled = canEdit;
        BrandLogoPathBox.IsEnabled = canEdit;
        BrandLogoPlacementCombo.IsEnabled = canEdit && showLogo;
        BrandLogoOffsetXSlider.IsEnabled = canEdit && showLogo;
        BrandLogoOffsetYSlider.IsEnabled = canEdit && showLogo;
        BrandLogoSizeSlider.IsEnabled = canEdit && showLogo;
        BrandLogoOpacitySlider.IsEnabled = canEdit && showLogo;
        BrandLogoTintEnabledCheck.IsEnabled = canEdit && showLogo;
        BrandLogoTintPanel.IsEnabled = canEdit && showLogo && BrandLogoTintEnabledCheck.IsChecked == true;
        BrandLogoSizeLabel.Text = $"{(int)BrandLogoSizeSlider.Value}%";
        BrandLogoOpacityLabel.Text = $"{(int)BrandLogoOpacitySlider.Value}%";
        BrandLogoOffsetXLabel.Text = $"{(int)BrandLogoOffsetXSlider.Value}";
        BrandLogoOffsetYLabel.Text = $"{(int)BrandLogoOffsetYSlider.Value}";
    }

    private ImageBrandSettings BuildPreviewImageBrandSettings()
    {
        var global = BuildImageBrandSettings();
        string? sampleFile = TryGetPreviewSourceImageFile(GetActiveSourceFolder());
        if (sampleFile is null)
            return global;

        return BrandLogoResolver.ResolveForFile(sampleFile, global, GetCurrentFolderLogoSettings());
    }

    private SourceFolderLogoSettings? GetFolderLogoSettings(string? folder)
    {
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            return null;

        var settings = SourceFolderLogoStore.GetForFolder(folder);
        settings.FolderPath = BrandLogoResolver.NormalizePath(folder);
        return settings;
    }

    private SourceFolderLogoSettings? GetCurrentFolderLogoSettings() =>
        GetFolderLogoSettings(GetActiveSourceFolder());

    private string? GetRootSourceFolder() => SourceFolderBox.Text?.Trim();

    private string? GetActiveSourceFolder()
    {
        if (SourceSubfolderCombo?.SelectedItem is FolderListItem item)
            return item.Path;
        return GetRootSourceFolder();
    }

    private IReadOnlyList<string> GetImmediateSubfolders(string rootFolder)
    {
        try
        {
            return Directory.GetDirectories(rootFolder)
                .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private void RefreshSubfolderCombo(string? rootFolder)
    {
        if (SourceSubfolderCombo is null)
            return;

        _updatingSourceFolderUi = true;
        try
        {
            SourceSubfolderCombo.ItemsSource = null;
            if (string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
            {
                SourceSubfolderCombo.IsEnabled = false;
                return;
            }

            var items = new List<FolderListItem>
            {
                new("Kök klasör", BrandLogoResolver.NormalizePath(rootFolder))
            };

            foreach (var dir in GetImmediateSubfolders(rootFolder))
                items.Add(new(Path.GetFileName(dir), BrandLogoResolver.NormalizePath(dir)));

            SourceSubfolderCombo.ItemsSource = items;
            SourceSubfolderCombo.SelectedIndex = 0;
            SourceSubfolderCombo.IsEnabled = items.Count > 1;
        }
        finally
        {
            _updatingSourceFolderUi = false;
        }
    }

    private void SourceSubfolderCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingSourceFolderUi)
            return;

        if (!_loadingPreset)
            PersistActiveFolderLogoSettings();

        _brandLogoEditingFilePath = null;
        SourceFileList.SelectedItems.Clear();
        RefreshImageCount();
        RefreshSourceFileList();
        UpdateOutputPreview();
        BrandLogoRenderer.ClearCache();
        if (!_loadingPreset)
            LoadBrandLogoUiForCurrentScope();
        ScheduleLivePreview();
        _lastActiveSourceFolder = GetActiveSourceFolder();
    }

    private void PersistBrandLogoForFolder(string folderPath)
    {
        if (_updatingBrandLogoUi || _loadingPreset || !IsBrandLogoUiReady())
            return;
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            return;

        var normalizedFolder = BrandLogoResolver.NormalizePath(folderPath);
        var settings = SourceFolderLogoStore.GetForFolder(normalizedFolder);
        settings.FolderPath = normalizedFolder;

        if (IsPerFileBrandLogoMode())
        {
            if (!string.IsNullOrEmpty(_brandLogoEditingFilePath)
                && IsFileInSourceFolder(_brandLogoEditingFilePath, normalizedFolder))
            {
                var key = BrandLogoResolver.NormalizePath(_brandLogoEditingFilePath);
                settings.PerFile[key] = ReadBrandLogoUiAsOverride();
            }
        }
        else
        {
            settings.FolderDefault = ReadBrandLogoUiAsOverride();
        }

        PrunePerFileEntries(settings, normalizedFolder);
        SourceFolderLogoStore.Save(settings);
        SyncGlobalBrandLogoStore(ReadBrandLogoUiAsSettings());
    }

    private string? GetActiveLogoEditFile()
    {
        if (!IsPerFileBrandLogoMode())
            return null;

        var folder = GetActiveSourceFolder();
        if (string.IsNullOrEmpty(folder))
            return null;

        if (SourceFileList.SelectedItems.Count == 1
            && SourceFileList.SelectedItems[0] is string single
            && IsFileInSourceFolder(single, folder))
        {
            return single;
        }

        if (!string.IsNullOrEmpty(_brandLogoEditingFilePath)
            && IsFileInSourceFolder(_brandLogoEditingFilePath, folder))
        {
            var activeKey = BrandLogoResolver.NormalizePath(_brandLogoEditingFilePath);
            foreach (var item in SourceFileList.SelectedItems)
            {
                if (item is string path && BrandLogoResolver.NormalizePath(path) == activeKey)
                    return _brandLogoEditingFilePath;
            }
        }

        foreach (var item in SourceFileList.SelectedItems)
        {
            if (item is string path && IsFileInSourceFolder(path, folder))
                return path;
        }

        return null;
    }

    private void RefreshProcessSelectionHint()
    {
        if (ProcessSelectionHint is null)
            return;

        ProcessSelectionHint.Text = IsPerFileBrandLogoMode()
            ? "İşlem: yalnızca listede seçili dosyalar. Logo: seçili dosyalardan birini düzenleyin."
            : "İşlem: bu klasördeki tüm görseller (alt klasörler hariç). Logo ayarı aynı kapsama uygulanır.";
    }

    private void ProcessSelectedOnlyCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingPreset)
            return;

        OnProcessSelectionModeChanged();
    }

    private void OnProcessSelectionModeChanged()
    {
        if (!IsBrandLogoUiReady())
            return;

        if (IsPerFileBrandLogoMode())
        {
            SaveGlobalBrandLogoFromUi();
            LoadBrandLogoUiForCurrentScope();
        }
        else
        {
            if (!string.IsNullOrEmpty(_brandLogoEditingFilePath))
                PersistBrandLogoFromUi(_brandLogoEditingFilePath);
            _brandLogoEditingFilePath = null;
            LoadBrandLogoUiForCurrentScope();
        }

        RefreshProcessSelectionHint();
        RefreshBrandLogoScopeHint();
        RefreshBrandLogoUi();
        ScheduleLivePreview();
    }

    private bool IsBrandLogoUiReady() =>
        ShowBrandLogoCheck is not null
        && BrandLogoPathBox is not null
        && BrandLogoSelectedHint is not null
        && BrandLogoPlacementCombo is not null
        && BrandLogoSizeSlider is not null
        && BrandLogoOpacitySlider is not null
        && BrandLogoOffsetXSlider is not null
        && BrandLogoOffsetYSlider is not null
        && BrandLogoTintEnabledCheck is not null
        && BrandLogoTintFillModeCombo is not null
        && BrandLogoPanel is not null;

    private void RefreshBrandLogoSelectedHint(string? path)
    {
        if (BrandLogoSelectedHint is null)
            return;

        if (string.IsNullOrWhiteSpace(path))
        {
            BrandLogoSelectedHint.Text = "Seçili logo: (yok)";
            return;
        }

        string fileName;
        try { fileName = Path.GetFileName(path); } catch { fileName = path; }

        // filigram-08/09 = dikey presetler
        if (fileName.Equals(BrandLogoCatalog.WhiteFileName, StringComparison.OrdinalIgnoreCase))
        {
            BrandLogoSelectedHint.Text = "Seçili logo: Dikey · Beyaz (filigram-08)";
            return;
        }
        if (fileName.Equals(BrandLogoCatalog.BlackFileName, StringComparison.OrdinalIgnoreCase))
        {
            BrandLogoSelectedHint.Text = "Seçili logo: Dikey · Siyah (filigram-09)";
            return;
        }

        // nadir-figur-yatay-* = yatay header logolar (burada da seçilirse onu gösterelim)
        if (fileName.Contains("yatay", StringComparison.OrdinalIgnoreCase))
        {
            string color = fileName.Contains("siyah", StringComparison.OrdinalIgnoreCase) ? "Siyah"
                : fileName.Contains("beyaz", StringComparison.OrdinalIgnoreCase) ? "Beyaz"
                : "Özel";
            BrandLogoSelectedHint.Text = $"Seçili logo: Yatay · {color} ({fileName})";
            return;
        }

        BrandLogoSelectedHint.Text = $"Seçili logo: Özel ({fileName})";
    }

    private void BindBrandLogoUiFromSettings(ImageBrandSettings settings)
    {
        ShowBrandLogoCheck.IsChecked = settings.ShowBrandLogo;
        var resolved = BrandLogoCatalog.ResolvePath(settings.BrandLogoPresetId, settings.BrandLogoPath) ?? settings.BrandLogoPath ?? "";
        BrandLogoPathBox.Text = resolved;
        RefreshBrandLogoSelectedHint(resolved);
        BrandLogoSizeSlider.Value = settings.BrandLogoSizePercent;
        BrandLogoOpacitySlider.Value = settings.BrandLogoOpacity * 100;
        BrandLogoOffsetXSlider.Value = settings.BrandLogoOffsetX;
        BrandLogoOffsetYSlider.Value = settings.BrandLogoOffsetY;
        BrandLogoTintEnabledCheck.IsChecked = settings.BrandLogoTintEnabled;
        _brandLogoTint = settings.BrandLogoTint.Clone();
        SelectPlacementCombo(BrandLogoPlacementCombo, settings.BrandLogoPlacement);
        BindBrandLogoTintUi();
    }

    private void BindBrandLogoUiFromOverride(FileBrandLogoOverride o)
    {
        ShowBrandLogoCheck.IsChecked = o.Enabled;
        var path = BrandLogoCatalog.ResolvePath(o.LogoPresetId, o.LogoPath) ?? o.LogoPath ?? "";
        BrandLogoPathBox.Text = path;
        RefreshBrandLogoSelectedHint(path);
        BrandLogoSizeSlider.Value = o.SizePercent;
        BrandLogoOpacitySlider.Value = o.Opacity * 100;
        BrandLogoOffsetXSlider.Value = o.OffsetX;
        BrandLogoOffsetYSlider.Value = o.OffsetY;
        BrandLogoTintEnabledCheck.IsChecked = o.BrandLogoTintEnabled;
        _brandLogoTint = o.BrandLogoTint.Clone();
        SelectPlacementCombo(
            BrandLogoPlacementCombo,
            Enum.TryParse<OverlayPlacement>(o.PlacementId, out var placement)
                ? placement
                : OverlayPlacement.BottomRight);
        BindBrandLogoTintUi();
    }

    private ImageBrandSettings ReadBrandLogoUiAsSettings()
    {
        var baseSettings = ImageBrandStore.Current.Clone();
        if (!IsBrandLogoUiReady())
            return baseSettings;

        baseSettings.ShowBrandLogo = ShowBrandLogoCheck.IsChecked == true;
        baseSettings.BrandLogoPresetId = BrandLogoCatalog.DetectPresetId(
            string.IsNullOrWhiteSpace(BrandLogoPathBox.Text) ? null : BrandLogoPathBox.Text.Trim());
        baseSettings.BrandLogoPath = string.IsNullOrWhiteSpace(BrandLogoPathBox.Text)
            ? null
            : BrandLogoPathBox.Text.Trim();
        baseSettings.BrandLogoSizePercent = (int)BrandLogoSizeSlider.Value;
        baseSettings.BrandLogoOpacity = (float)(BrandLogoOpacitySlider.Value / 100.0);
        baseSettings.BrandLogoPlacement = BrandLogoPlacementCombo.SelectedItem is PlacementListItem p
            ? p.Placement
            : OverlayPlacement.BottomRight;
        baseSettings.BrandLogoOffsetX = (int)BrandLogoOffsetXSlider.Value;
        baseSettings.BrandLogoOffsetY = (int)BrandLogoOffsetYSlider.Value;
        baseSettings.BrandLogoTintEnabled = BrandLogoTintEnabledCheck.IsChecked == true;
        baseSettings.BrandLogoTint = ReadBrandLogoTintFromUi().Clone();
        return baseSettings;
    }

    private FileBrandLogoOverride ReadBrandLogoUiAsOverride()
    {
        var settings = ReadBrandLogoUiAsSettings();
        return BrandLogoResolver.CreateOverrideFromSettings(settings);
    }

    private void SaveGlobalBrandLogoFromUi()
    {
        if (_updatingBrandLogoUi || _loadingPreset || !IsBrandLogoUiReady())
            return;

        var logoSettings = ReadBrandLogoUiAsSettings();
        SyncGlobalBrandLogoStore(logoSettings);

        var folderSettings = GetCurrentFolderLogoSettings();
        if (folderSettings is not null)
        {
            folderSettings.FolderDefault = ReadBrandLogoUiAsOverride();
            PrunePerFileEntries(folderSettings, folderSettings.FolderPath!);
            SourceFolderLogoStore.Save(folderSettings);
            return;
        }

        ImageBrandStore.Save(logoSettings);
    }

    private void PersistBrandLogoFromUi(string? filePath = null)
    {
        if (_updatingBrandLogoUi || _loadingPreset || !IsBrandLogoUiReady())
            return;

        if (!IsPerFileBrandLogoMode())
        {
            SaveGlobalBrandLogoFromUi();
            return;
        }

        var targetFile = filePath ?? GetActiveLogoEditFile();
        if (targetFile is null)
            return;

        var folder = GetCurrentFolderLogoSettings();
        if (folder is null)
        {
            if (!IsPerFileBrandLogoMode())
                ImageBrandStore.Save(ReadBrandLogoUiAsSettings());
            return;
        }

        var key = BrandLogoResolver.NormalizePath(targetFile);
        if (!IsFileInSourceFolder(targetFile, folder.FolderPath!))
            return;

        folder.PerFile[key] = ReadBrandLogoUiAsOverride();
        folder.FolderDefault ??= folder.PerFile[key].Clone();
        PrunePerFileEntries(folder, folder.FolderPath!);
        SourceFolderLogoStore.Save(folder);
        SyncGlobalBrandLogoStore(ReadBrandLogoUiAsSettings());
    }

    private void LoadBrandLogoUiForCurrentScope()
    {
        if (!IsBrandLogoUiReady())
            return;

        _updatingBrandLogoUi = true;
        try
        {
            if (!IsPerFileBrandLogoMode())
            {
                var folderSettings = GetCurrentFolderLogoSettings();
                if (folderSettings?.FolderDefault is not null)
                {
                    BindBrandLogoUiFromOverride(folderSettings.FolderDefault);
                }
                else
                {
                    var brand = ImageBrandStore.Load();
                    if (string.IsNullOrWhiteSpace(brand.BrandLogoPath) && File.Exists(BrandLogoCatalog.WhiteLogoPath))
                        brand.BrandLogoPath = BrandLogoCatalog.WhiteLogoPath;
                    BindBrandLogoUiFromSettings(brand);
                }

                _brandLogoEditingFilePath = null;
                return;
            }

            var selected = GetActiveLogoEditFile();
            _brandLogoEditingFilePath = selected;
            if (selected is null)
                return;

            var folder = GetActiveSourceFolder();
            if (folder is null || !IsFileInSourceFolder(selected, folder))
                return;

            var perFileFolderSettings = GetCurrentFolderLogoSettings();
            var key = BrandLogoResolver.NormalizePath(selected);
            if (perFileFolderSettings?.PerFile.TryGetValue(key, out var perFile) == true)
                BindBrandLogoUiFromOverride(perFile);
            else if (perFileFolderSettings?.FolderDefault is not null)
                BindBrandLogoUiFromOverride(perFileFolderSettings.FolderDefault);
            else
                BindBrandLogoUiFromSettings(ImageBrandStore.Load());
        }
        finally
        {
            _updatingBrandLogoUi = false;
            RefreshBrandLogoUi();
            RefreshBrandLogoScopeHint();
        }
    }

    private void RefreshBrandLogoScopeHint()
    {
        if (BrandLogoScopeHint is null)
            return;

        if (!IsPerFileBrandLogoMode())
        {
            BrandLogoScopeHint.Text = "Logo ayarları bu klasördeki görsellere uygulanır (alt klasörler dahil değil).";
            return;
        }

        var selected = GetActiveLogoEditFile();
        BrandLogoScopeHint.Text = selected is null
            ? "Dosya başına logo: listeden en az bir dosya seçin."
            : $"Düzenlenen: {Path.GetFileName(selected)} — başka dosya için listeden seçin.";
    }

    private void BrandLogoUi_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingBrandLogoUi || !_previewReady || !IsBrandLogoUiReady())
            return;

        RefreshBrandLogoUi();
        BrandLogoRenderer.ClearCache();
        PersistBrandLogoFromUi();
        ScheduleLivePreview();
    }

    private void BrandLogoSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingBrandLogoUi || !_previewReady || !IsBrandLogoUiReady())
            return;

        RefreshBrandLogoUi();
        BrandLogoRenderer.ClearCache();
        PersistBrandLogoFromUi();
        ScheduleLivePreview();
    }

    private void ApplyBrandLogoPreset(string presetId)
    {
        BrandLogoCatalog.EnsureBundledLogos();
        var path = BrandLogoCatalog.ResolvePath(presetId, null);
        if (path is null)
            return;

        _updatingBrandLogoUi = true;
        ShowBrandLogoCheck.IsChecked = true;
        BrandLogoPathBox.Text = path;
        _updatingBrandLogoUi = false;
        RefreshBrandLogoSelectedHint(path);
        RefreshBrandLogoUi();
        BrandLogoRenderer.ClearCache();
        PersistBrandLogoFromUi();
        ScheduleLivePreview();
    }

    private void BrandLogoWhitePreset_Click(object sender, RoutedEventArgs e) =>
        ApplyBrandLogoPreset(BrandLogoCatalog.WhitePresetId);

    private void BrandLogoBlackPreset_Click(object sender, RoutedEventArgs e) =>
        ApplyBrandLogoPreset(BrandLogoCatalog.BlackPresetId);

    private void BrandLogoHorizontalWhitePreset_Click(object sender, RoutedEventArgs e) =>
        ApplyBrandLogoPreset(BrandLogoCatalog.HorizontalWhitePresetId);

    private void BrandLogoHorizontalBlackPreset_Click(object sender, RoutedEventArgs e) =>
        ApplyBrandLogoPreset(BrandLogoCatalog.HorizontalBlackPresetId);

    private void ApplyBrandLogoToAll_Click(object sender, RoutedEventArgs e)
    {
        if (SourceFileList.Items.Count == 0)
            return;

        var folderSettings = GetCurrentFolderLogoSettings();
        if (folderSettings is null)
        {
            ImageBrandStore.Save(ReadBrandLogoUiAsSettings());
            return;
        }

        var template = ReadBrandLogoUiAsOverride();
        folderSettings.FolderDefault = template.Clone();
        var folderPath = folderSettings.FolderPath!;
        foreach (var item in SourceFileList.Items)
        {
            if (item is not string file || !IsFileInSourceFolder(file, folderPath))
                continue;
            folderSettings.PerFile[BrandLogoResolver.NormalizePath(file)] = template.Clone();
        }

        PrunePerFileEntries(folderSettings, folderPath);
        SourceFolderLogoStore.Save(folderSettings);
        SyncGlobalBrandLogoStore(ReadBrandLogoUiAsSettings());
        BrandLogoRenderer.ClearCache();
        ScheduleLivePreview();
    }

    private void BrowseBrandLogo_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Marka logosu seçin",
            Filter = LogoImageLoader.OpenFileDialogFilter,
            InitialDirectory = BrandLogoCatalog.AssetsFolder
        };

        if (dialog.ShowDialog() != true)
            return;

        _updatingBrandLogoUi = true;
        ShowBrandLogoCheck.IsChecked = true;
        BrandLogoPathBox.Text = dialog.FileName;
        _updatingBrandLogoUi = false;
        RefreshBrandLogoSelectedHint(dialog.FileName);
        RefreshBrandLogoUi();
        BrandLogoRenderer.ClearCache();
        PersistBrandLogoFromUi();
        ScheduleLivePreview();
    }

    private void OnSourceFileSelectionChanged()
    {
        if (IsPerFileBrandLogoMode())
        {
            var next = GetActiveLogoEditFile();
            if (!string.IsNullOrEmpty(_brandLogoEditingFilePath)
                && next is not null
                && !string.Equals(
                    BrandLogoResolver.NormalizePath(_brandLogoEditingFilePath),
                    BrandLogoResolver.NormalizePath(next),
                    StringComparison.OrdinalIgnoreCase))
            {
                PersistBrandLogoFromUi(_brandLogoEditingFilePath);
            }

            LoadBrandLogoUiForCurrentScope();
        }
        else
        {
            RefreshBrandLogoScopeHint();
        }

        ScheduleLivePreview();
    }

    private static void SelectPlacementCombo(ComboBox combo, OverlayPlacement placement)
    {
        foreach (PlacementListItem item in combo.Items)
        {
            if (item.Placement == placement)
            {
                combo.SelectedItem = item;
                return;
            }
        }

        if (combo.Items.Count > 0)
            combo.SelectedIndex = 0;
    }

    private void BrandFont_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingBrandFields || _loadingPreset)
            return;
        PersistImageBrandSettings();
        ScheduleLivePreview();
    }

    private void PersistImageBrandSettings()
    {
        if (_updatingBrandFields || _loadingPreset || !IsImageBrandUiReady())
            return;

        var built = BuildImageBrandSettings();
        if (IsPerFileBrandLogoMode())
        {
            var current = ImageBrandStore.Load();
            current.MainText = built.MainText;
            current.SuffixText = built.SuffixText;
            current.MainFontId = built.MainFontId;
            current.SuffixFontId = built.SuffixFontId;
            current.ShowMainText = built.ShowMainText;
            current.ShowSuffixText = built.ShowSuffixText;
            current.MainTextSizePercent = built.MainTextSizePercent;
            current.SuffixTextSizePercent = built.SuffixTextSizePercent;
            ImageBrandStore.Save(current);
            return;
        }

        ImageBrandStore.Save(built);
    }

    private void ImageBrand_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingBrandFields || _loadingPreset)
            return;
        RefreshColorFieldLabels();
        PersistImageBrandSettings();
        ScheduleLivePreview();
    }

    private void CopyBrandFromHeader_Click(object sender, RoutedEventArgs e)
    {
        var header = HeaderBrandingStore.Current;
        _updatingBrandFields = true;
        ImageBrandMainBox.Text = header.MainText;
        ImageBrandSuffixBox.Text = header.SuffixText;
        _updatingBrandFields = false;
        RefreshColorFieldLabels();
        PersistImageBrandSettings();
        ScheduleLivePreview();
    }

    private ExportResolutionProfile GetSelectedExportProfile() =>
        ExportResolutionCombo.SelectedItem is ExportResolutionListItem item
            ? item.Profile
            : ExportResolutionRegistry.Default;

    private void ExportResolutionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshExportResolutionHint();
        ScheduleLivePreview();
    }

    private void RefreshExportResolutionHint()
    {
        if (ExportResolutionCombo.SelectedItem is ExportResolutionListItem item)
            ExportResolutionHint.Text = item.Description;
        else
            ExportResolutionHint.Text = "";
    }

    private void OpenBrandingSettings()
    {
        var dialog = new HeaderBrandingWindow(HeaderBrandingStore.Current, this);
        if (dialog.ShowDialog() == true)
        {
            BrandingHeader.RefreshBranding();
            ScheduleLivePreview();
        }
    }

    private void InitializeLogoPath()
    {
        var settings = LogoPathSettingsStore.Load();
        _customLogoPath = settings.CustomLogoPath;

        _updatingLogoPathUi = true;
        UseDefaultLogoCheck.IsChecked = settings.UseDefaultLogo;
        _updatingLogoPathUi = false;

        RefreshLogoPathUi();
    }

    private void UseDefaultLogoCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingLogoPathUi)
            return;

        RefreshLogoPathUi();
        PersistLogoPathSettings();
        ScheduleLivePreview();
    }

    private void RefreshLogoPathUi()
    {
        if (UseDefaultLogoCheck is null || LogoPathBox is null || BrowseLogoButton is null || LogoModeCombo is null)
            return;

        bool useDefault = UseDefaultLogoCheck.IsChecked == true;

        BrowseLogoButton.IsEnabled = !useDefault;
        LogoPathBox.IsReadOnly = true;

        if (useDefault)
        {
            var resolved = LogoProvider.ResolveLogoPath(null);
            var path = resolved ?? LogoProvider.DefaultLogoPath;
            LogoPathBox.Text = path;
            UpdateLogoFormatLabel(path);
            LogoPathBox.ToolTip = "İşlemde bu yoldaki logo kullanılır (varsayılan).";
            return;
        }

        LogoPathBox.ToolTip = "Logo seç… ile belirlediğiniz dosya işlemde kullanılır.";
        if (!string.IsNullOrWhiteSpace(_customLogoPath))
        {
            LogoPathBox.Text = _customLogoPath;
            UpdateLogoFormatLabel(_customLogoPath);
        }
        else
        {
            LogoPathBox.Text = "(Logo seç… ile dosya gösterin)";
            if (LogoFormatLabel is not null)
                LogoFormatLabel.Text = "";
        }
    }

    private void UpdateLogoFormatLabel(string? path)
    {
        if (LogoFormatLabel is null)
            return;

        if (string.IsNullOrWhiteSpace(path) || path.StartsWith('('))
        {
            LogoFormatLabel.Text = "";
            return;
        }

        try
        {
            using var loaded = LogoProvider.LoadDetails(
                UsesDefaultLogo() ? null : path);
            string effective = loaded.EffectivePath;
            LogoFormatLabel.Text = loaded.Kind is LogoFileKind.Png or LogoFileKind.Svg
                ? $"Format: {loaded.FormatLabel} (şeffaflık korunur) — {Path.GetFileName(effective)}"
                : loaded.Kind == LogoFileKind.Jpeg
                    ? $"Format: JPEG — {Path.GetFileName(effective)}"
                    : $"Format: {loaded.FormatLabel} — {Path.GetFileName(effective)}";
        }
        catch
        {
            LogoFormatLabel.Text = $"Format: {LogoImageLoader.GetFormatLabelForPath(path)}";
        }
    }

    private void PersistLogoPathSettings()
    {
        LogoPathSettingsStore.Save(new LogoPathSettings
        {
            UseDefaultLogo = UseDefaultLogoCheck.IsChecked == true,
            CustomLogoPath = _customLogoPath
        });
    }

    private bool UsesDefaultLogo() => UseDefaultLogoCheck.IsChecked == true;

    private string? ResolveActiveLogoPath()
    {
        if (UsesDefaultLogo())
            return null;

        return string.IsNullOrWhiteSpace(_customLogoPath) ? null : _customLogoPath.Trim();
    }

    private void TemplateCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TemplateCombo.SelectedItem is TemplateListItem item)
        {
            TemplateDescription.Text = item.Template.IsPassthrough
                ? item.Description
                : $"{item.Description}\nBoyut: {item.SizeLabel}";
            TemplateFavoritesStore.TouchRecent(item.Template.Id);
            UpdateFavoriteButton();
            RefreshTemplateDependantUi(item);
            RefreshResponsiveFitUi();
        }
        RefreshExportResolutionComboItems();
        ScheduleLivePreview();
    }

    private void RefreshTemplateDependantUi(TemplateListItem item)
    {
        if (ResizeOnlyCheck is null)
            return;

        if (item.Template.IsPassthrough)
        {
            ResizeOnlyCheck.IsChecked = false;
            ResizeOnlyCheck.IsEnabled = false;
        }
        else
        {
            ResizeOnlyCheck.IsEnabled = true;
        }
    }

    private void RefreshTemplateComboItems(string? selectId = null)
    {
        string? currentId = selectId
            ?? (TemplateCombo.SelectedItem is TemplateListItem cur ? cur.Template.Id : null);

        var data = TemplateFavoritesStore.Load();
        var all = TemplateRegistry.Templates.Select(t => new TemplateListItem(t)).ToList();
        var ordered = new List<TemplateListItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddIfFound(string id)
        {
            if (seen.Contains(id)) return;
            var item = all.FirstOrDefault(x => x.Template.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (item is null) return;
            ordered.Add(item);
            seen.Add(id);
        }

        foreach (var id in data.FavoriteIds)
            AddIfFound(id);
        foreach (var id in data.RecentIds)
            AddIfFound(id);
        foreach (var item in all)
            AddIfFound(item.Template.Id);

        TemplateCombo.ItemsSource = ordered;
        if (!string.IsNullOrEmpty(currentId))
        {
            var match = ordered.FirstOrDefault(x => x.Template.Id == currentId);
            if (match is not null)
                TemplateCombo.SelectedItem = match;
        }
        if (TemplateCombo.SelectedIndex < 0 && ordered.Count > 0)
            TemplateCombo.SelectedIndex = 0;
        UpdateFavoriteButton();
    }

    private void UpdateFavoriteButton()
    {
        if (FavoriteTemplateButton is null || TemplateCombo.SelectedItem is not TemplateListItem item)
            return;
        bool fav = TemplateFavoritesStore.IsFavorite(item.Template.Id);
        FavoriteTemplateButton.Content = fav ? "★" : "☆";
        FavoriteTemplateButton.ToolTip = fav ? "Favoriden çıkar" : "Favoriye ekle";
    }

    private void FavoriteTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (TemplateCombo.SelectedItem is not TemplateListItem item)
            return;
        TemplateFavoritesStore.ToggleFavorite(item.Template.Id);
        string id = item.Template.Id;
        RefreshTemplateComboItems(selectId: id);
    }

    private void LoadPresets()
    {
        _presets = ProcessingPresetStore.LoadAll();
        PresetCombo.ItemsSource = null;
        PresetCombo.ItemsSource = _presets;
        if (_presets.Count > 0)
        {
            PresetCombo.SelectedIndex = 0;
            PresetNameBox.Text = _presets[0].Name;
        }
    }

    private void PresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingPreset || PresetCombo.SelectedItem is not ProcessingPreset preset)
            return;
        ApplyPreset(preset);
        PresetNameBox.Text = preset.Name;
    }

    private void ApplyPreset(ProcessingPreset preset)
    {
        _loadingPreset = true;
        try
        {
            RefreshTemplateComboItems(selectId: preset.TemplateId);
            SelectComboByTag(ColorPackCombo, preset.ColorPackId, p => p is ColorPackListItem c ? c.Theme.Id : "");
            if (preset.ColorPackId == "ozel")
            {
                _customBackgroundHex = preset.CustomBackgroundHex;
                _customRonekaiHex = preset.CustomRonekaiHex;
                _customDenHex = preset.CustomDenHex;
            }

            _themeColors = preset.ThemeColors?.Clone()
                           ?? ThemeColorSet.FromHex(preset.CustomBackgroundHex, preset.CustomRonekaiHex, preset.CustomDenHex);
            BindColorAppearanceUi();
            RefreshColorPackUi();
            SelectComboByTag(ExportResolutionCombo, preset.ExportProfileId,
                p => p is ExportResolutionListItem e ? e.Profile.Id : "");
            SelectComboByTag(LogoModeCombo, preset.LogoModeId,
                p => p is LogoModeListItem l ? l.Mode.ToString() : "");
            _updatingLogoPathUi = true;
            UseDefaultLogoCheck.IsChecked = preset.UseDefaultLogo;
            _customLogoPath = preset.CustomLogoPath;
            _updatingLogoPathUi = false;
            LogoOpacitySlider.Value = preset.LogoOpacity * 100;
            SelectPlacementCombo(LogoPlacementCombo,
                OverlayPlacementRegistry.Parse(preset.LogoPlacementId, OverlayPlacement.Center));
            LogoScaleSlider.Value = preset.LogoScalePercent > 0 ? preset.LogoScalePercent : 62;
            _updatingBrandFields = true;
            ImageBrandMainBox.Text = preset.ImageBrandMain;
            ImageBrandSuffixBox.Text = preset.ImageBrandSuffix;
            SelectFontCombo(BrandMainFontCombo, preset.BrandMainFontId);
            SelectFontCombo(BrandSuffixFontCombo, preset.BrandSuffixFontId);
            ShowImageBrandMainCheck.IsChecked = preset.ImageBrandShowMain;
            ShowImageBrandSuffixCheck.IsChecked = preset.ImageBrandShowSuffix;
            ImageBrandMainSizeSlider.Value = preset.ImageBrandMainSizePercent;
            ImageBrandSuffixSizeSlider.Value = preset.ImageBrandSuffixSizePercent;
            _updatingBrandLogoUi = true;
            ShowBrandLogoCheck.IsChecked = preset.ImageBrandShowLogo;
            BrandLogoPathBox.Text = preset.ImageBrandLogoPath ?? "";
            BrandLogoSizeSlider.Value = preset.ImageBrandLogoSizePercent;
            BrandLogoOpacitySlider.Value = preset.ImageBrandLogoOpacity * 100;
            BrandLogoOffsetXSlider.Value = preset.BrandLogoOffsetX;
            BrandLogoOffsetYSlider.Value = preset.BrandLogoOffsetY;
            BrandLogoTintEnabledCheck.IsChecked = preset.BrandLogoTintEnabled;
            _brandLogoTint = preset.BrandLogoTint?.Clone()
                             ?? ThemeColorAppearance.FromHex("#1B2A4A", "#C9A227");
            SelectPlacementCombo(BrandLogoPlacementCombo,
                OverlayPlacementRegistry.Parse(preset.ImageBrandLogoPlacementId, OverlayPlacement.BottomRight));
            BindBrandLogoTintUi();
            _updatingBrandLogoUi = false;
            _updatingBrandFields = false;
            BrandLogoRenderer.ClearCache();
            RefreshImageBrandSizeUi();
            RefreshBrandLogoUi();
            PersistImageBrandSettings();
            PersistBrandLogoFromUi();
            ResizeOnlyCheck.IsChecked = preset.ResizeOnly;
            ResponsiveProductFitCheck.IsChecked = preset.ResponsiveProductFit;
            JpegQualitySlider.Value = preset.JpegQuality;
            SaveAsPngCheck.IsChecked = preset.SaveAsPng;
            OutputFileNameBox.Text = string.IsNullOrWhiteSpace(preset.FileNamePattern)
                ? OutputFileNamer.DefaultPattern
                : preset.FileNamePattern;
            TextOverlayEnabledCheck.IsChecked = preset.TextOverlayEnabled;
            TextOverlayTextBox.Text = preset.TextOverlayText;
            TextOverlayTextBox.IsEnabled = preset.TextOverlayEnabled;
            SelectTextOverlayPosition(preset.TextOverlayPosition);
            SamplePreviewCountBox.Text = preset.SamplePreviewCount.ToString();
            ProcessSelectedOnlyCheck.IsChecked = preset.ProcessSelectedOnly;
            if (!string.IsNullOrWhiteSpace(preset.SourceFolderPath) && Directory.Exists(preset.SourceFolderPath))
                SourceFolderBox.Text = preset.SourceFolderPath;
            RefreshLogoPathUi();
            RefreshLogoModeUi();
            RefreshResponsiveFitUi();
            ScheduleLivePreview();
        }
        finally
        {
            _loadingPreset = false;
        }
    }

    private static void SelectComboByTag(ComboBox combo, string id, Func<object, string> getId)
    {
        foreach (var item in combo.Items)
        {
            if (getId(item).Equals(id, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = item;
                return;
            }
        }
    }

    private ProcessingPreset CaptureCurrentPreset(string name)
    {
        var templateId = TemplateCombo.SelectedItem is TemplateListItem t ? t.Template.Id : "white-studio";
        var colorId = ColorPackCombo.SelectedItem is ColorPackListItem c ? c.Theme.Id : "klasik";
        var exportId = ExportResolutionCombo.SelectedItem is ExportResolutionListItem e ? e.Profile.Id : "template-default";
        var logoId = LogoModeCombo.SelectedItem is LogoModeListItem l ? l.Mode.ToString() : "None";
        var brand = BuildImageBrandSettings();
        return new ProcessingPreset
        {
            Name = name,
            TemplateId = templateId,
            ColorPackId = colorId,
            ExportProfileId = exportId,
            LogoModeId = logoId,
            UseDefaultLogo = UseDefaultLogoCheck.IsChecked == true,
            CustomLogoPath = _customLogoPath,
            LogoOpacity = (float)(LogoOpacitySlider.Value / 100.0),
            LogoPlacementId = LogoPlacementCombo.SelectedItem is PlacementListItem lp
                ? lp.Placement.ToString()
                : OverlayPlacement.Center.ToString(),
            LogoScalePercent = (int)LogoScaleSlider.Value,
            ImageBrandMain = brand.MainText,
            ImageBrandSuffix = brand.SuffixText,
            BrandMainFontId = brand.MainFontId,
            BrandSuffixFontId = brand.SuffixFontId,
            ImageBrandShowMain = brand.ShowMainText,
            ImageBrandShowSuffix = brand.ShowSuffixText,
            ImageBrandMainSizePercent = brand.MainTextSizePercent,
            ImageBrandSuffixSizePercent = brand.SuffixTextSizePercent,
            ImageBrandShowLogo = brand.ShowBrandLogo,
            ImageBrandLogoPath = brand.BrandLogoPath,
            ImageBrandLogoSizePercent = brand.BrandLogoSizePercent,
            ImageBrandLogoOpacity = brand.BrandLogoOpacity,
            ImageBrandLogoPlacementId = brand.BrandLogoPlacement.ToString(),
            BrandLogoOffsetX = brand.BrandLogoOffsetX,
            BrandLogoOffsetY = brand.BrandLogoOffsetY,
            BrandLogoTintEnabled = brand.BrandLogoTintEnabled,
            BrandLogoTint = brand.BrandLogoTint.Clone(),
            CustomBackgroundHex = _customBackgroundHex,
            CustomRonekaiHex = _customRonekaiHex,
            CustomDenHex = _customDenHex,
            ThemeColors = BuildThemeColorSet().Clone(),
            ResizeOnly = ResizeOnlyCheck.IsChecked == true,
            JpegQuality = (int)JpegQualitySlider.Value,
            SaveAsPng = SaveAsPngCheck.IsChecked == true,
            FileNamePattern = OutputFileNameBox.Text.Trim(),
            TextOverlayEnabled = TextOverlayEnabledCheck.IsChecked == true,
            TextOverlayText = TextOverlayTextBox.Text ?? "",
            TextOverlayPosition = GetSelectedTextOverlayPositionId(),
            SamplePreviewCount = ParseSampleCount(),
            ProcessSelectedOnly = ProcessSelectedOnlyCheck.IsChecked == true,
            SourceFolderPath = SourceFolderBox.Text?.Trim()
        };
    }

    private void SavePreset_Click(object sender, RoutedEventArgs e)
    {
        string name = PresetNameBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("Profil adı girin.", "PhonixFrame", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var preset = CaptureCurrentPreset(name);
        var existing = _presets.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            _presets.Remove(existing);
        _presets.Add(preset);
        ProcessingPresetStore.SaveAll(_presets);
        LoadPresets();
        PresetCombo.SelectedItem = _presets.FirstOrDefault(p => p.Name == name) ?? preset;
        MessageBox.Show($"Profil kaydedildi: {name}", "PhonixFrame", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void DeletePreset_Click(object sender, RoutedEventArgs e)
    {
        if (PresetCombo.SelectedItem is not ProcessingPreset preset)
            return;
        if (_presets.Count <= 1)
        {
            MessageBox.Show("En az bir profil kalmalı.", "PhonixFrame", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _presets.RemoveAll(p => p.Name == preset.Name);
        ProcessingPresetStore.SaveAll(_presets);
        LoadPresets();
    }

    private ProcessingJobSettings BuildJobSettings()
    {
        var template = TemplateCombo.SelectedItem is TemplateListItem t ? t.Template : null;
        bool resizeOnly = ResizeOnlyCheck.IsChecked == true;
        return new()
        {
            ResizeOnly = resizeOnly,
            StretchToExport = template?.StretchToExport == true && !resizeOnly,
            ResponsiveProductFit = ResponsiveProductFitCheck.IsChecked == true && !resizeOnly,
            JpegQuality = (int)JpegQualitySlider.Value,
            SaveAsPng = SaveAsPngCheck.IsChecked == true,
            FileNamePattern = string.IsNullOrWhiteSpace(OutputFileNameBox.Text)
                ? OutputFileNamer.DefaultPattern
                : OutputFileNameBox.Text.Trim(),
            TextOverlay = BuildTextOverlaySettings(),
            SamplePreviewCount = ParseSampleCount(),
            ProcessOnlySelectedFiles = ProcessSelectedOnlyCheck.IsChecked == true
        };
    }

    private void SelectTextOverlayPosition(string positionId)
    {
        foreach (var item in TextOverlayPositionCombo.Items)
        {
            if (item is ComboTextItem c && c.Position.ToString().Equals(positionId, StringComparison.OrdinalIgnoreCase))
            {
                TextOverlayPositionCombo.SelectedItem = item;
                return;
            }
        }
    }

    private string GetSelectedTextOverlayPositionId() =>
        TextOverlayPositionCombo.SelectedItem is ComboTextItem item
            ? item.Position.ToString()
            : TextOverlayPosition.BottomCenter.ToString();

    private TextOverlaySettings BuildTextOverlaySettings()
    {
        var pos = TextOverlayPositionCombo.SelectedItem is ComboTextItem item
            ? item.Position
            : TextOverlayPosition.BottomCenter;
        return new TextOverlaySettings
        {
            Enabled = TextOverlayEnabledCheck.IsChecked == true,
            Text = TextOverlayTextBox.Text ?? "",
            Position = pos,
            Opacity = 0.85f
        };
    }

    private int ParseSampleCount()
    {
        if (!int.TryParse(SamplePreviewCountBox.Text?.Trim(), out int n) || n < 0)
            return 0;
        return Math.Min(n, 20);
    }

    private void JpegQualitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (JpegQualityLabel is not null)
            JpegQualityLabel.Text = $"{(int)JpegQualitySlider.Value}";
        ScheduleLivePreview();
    }

    private void AdvancedOption_Changed(object sender, RoutedEventArgs e)
    {
        RefreshResponsiveFitUi();
        ScheduleLivePreview();
    }

    private void RefreshResponsiveFitUi()
    {
        if (ResponsiveProductFitCheck is null || ResizeOnlyCheck is null)
            return;
        bool resizeOnly = ResizeOnlyCheck.IsChecked == true;
        bool passthrough = TemplateCombo.SelectedItem is TemplateListItem t && t.Template.IsPassthrough;
        bool stretch = TemplateCombo.SelectedItem is TemplateListItem t2 && t2.Template.StretchToExport;
        ResponsiveProductFitCheck.IsEnabled = !resizeOnly && !passthrough && !stretch;
    }

    private void RefreshFileList_Click(object sender, RoutedEventArgs e)
    {
        RefreshSourceFileList();
        ScheduleLivePreview();
    }

    private void SourceFileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_previewReady)
            return;

        OnSourceFileSelectionChanged();
    }

    private void SelectAllFiles_Click(object sender, RoutedEventArgs e)
    {
        if (SourceFileList.Items.Count > 0)
            SourceFileList.SelectAll();
    }

    private void RefreshSourceFileList()
    {
        SourceFileList.Items.Clear();
        var path = GetActiveSourceFolder();
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return;

        foreach (var file in BatchProcessor.FindImages(path))
            SourceFileList.Items.Add(file);
    }

    private void SourceDropZone_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void SourceDropZone_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        var paths = ((string[])e.Data.GetData(DataFormats.FileDrop)!)!;
        var folder = paths.FirstOrDefault(Directory.Exists);
        if (folder is not null)
        {
            SourceFolderBox.Text = folder;
            return;
        }

        var files = paths.Where(File.Exists)
            .Where(p => ImageInputCatalog.IsSupportedExtension(Path.GetExtension(p)))
            .ToList();
        if (files.Count == 0)
            return;

        var parent = Path.GetDirectoryName(files[0]);
        if (!string.IsNullOrEmpty(parent))
            SourceFolderBox.Text = parent;

        RefreshSourceFileList();
        SourceFileList.SelectedItems.Clear();
        foreach (var file in files)
        {
            for (int i = 0; i < SourceFileList.Items.Count; i++)
            {
                if (string.Equals(SourceFileList.Items[i]?.ToString(), file, StringComparison.OrdinalIgnoreCase))
                    SourceFileList.SelectedItems.Add(SourceFileList.Items[i]);
            }
        }
        ProcessSelectedOnlyCheck.IsChecked = files.Count > 0;
        OnProcessSelectionModeChanged();
        ScheduleLivePreview();
    }

    private sealed record ComboTextItem(string Label, TextOverlayPosition Position);
    private sealed record AppearanceComboItem(string Label, object Value);

    private void RefreshExportResolutionComboItems()
    {
        int? tw = null;
        int? th = null;
        if (TemplateCombo.SelectedItem is TemplateListItem templateItem && !templateItem.Template.IsPassthrough)
        {
            tw = templateItem.Template.OutputSize.Width;
            th = templateItem.Template.OutputSize.Height;
        }

        string? selectedId = ExportResolutionCombo.SelectedItem is ExportResolutionListItem current
            ? current.Profile.Id
            : null;

        var items = ExportResolutionRegistry.BuildListItems(tw, th);
        ExportResolutionCombo.ItemsSource = items;

        if (!string.IsNullOrEmpty(selectedId))
        {
            var match = items.FirstOrDefault(i => i.Profile.Id == selectedId);
            if (match is not null)
            {
                ExportResolutionCombo.SelectedItem = match;
                return;
            }
        }

        if (ExportResolutionCombo.SelectedIndex < 0 && items.Count > 0)
            ExportResolutionCombo.SelectedIndex = 0;
    }

    private void ColorPackCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ColorPackCombo.SelectedItem is not ColorPackListItem)
            return;

        RefreshColorPackUi();
        if (ColorPackCombo.SelectedItem is ColorPackListItem item && item.Theme.IsCustom)
            ColorSettingsPanel?.BringIntoView();
        ScheduleLivePreview();
    }

    private void RefreshColorPackUi()
    {
        if (ColorPackCombo.SelectedItem is not ColorPackListItem item)
            return;

        ColorPackDescription.Text = item.Theme.IsCustom
            ? item.Description
            : $"{item.Description}\n\n{BuildColorFieldsHint()}";

        if (item.Theme.IsCustom)
            ApplyColorPreviews(_customBackgroundHex, _customRonekaiHex, _customDenHex);
        else
        {
            _customBackgroundHex = item.Theme.BackgroundHex;
            _customRonekaiHex = item.Theme.RonekaiHex;
            _customDenHex = item.Theme.DenHex;
            _themeColors = ThemeColorSet.FromTheme(item.Theme);
            BindColorAppearanceUi();
            ApplyColorPreviews(item.Theme.BackgroundHex, item.Theme.RonekaiHex, item.Theme.DenHex);
        }
    }

    /// <summary>Hazır paletten özel moda geçer; mevcut palet renklerini özel alanlara kopyalar.</summary>
    private void ActivateCustomColorsFromSelection()
    {
        if (ColorPackCombo.SelectedItem is ColorPackListItem item && !item.Theme.IsCustom)
        {
            _customBackgroundHex = item.Theme.BackgroundHex;
            _customRonekaiHex = item.Theme.RonekaiHex;
            _customDenHex = item.Theme.DenHex;
            _themeColors = ThemeColorSet.FromTheme(item.Theme);
        }

        SelectCustomColorPack();
    }

    private void ApplyColorPreviews(string bg, string ronekai, string den)
    {
        _updatingColorFields = true;
        SyncThemeColorHexFromFields(bg, ronekai, den);
        BackgroundColorPreview.Background = AppearanceBrushHelper.ToPreviewBrush(_themeColors.Background);
        RonekaiColorPreview.Background = AppearanceBrushHelper.ToPreviewBrush(_themeColors.MainText);
        DenColorPreview.Background = AppearanceBrushHelper.ToPreviewBrush(_themeColors.Suffix);

        SetColorInputBoxes(BackgroundColorHexBox, BackgroundColorRgbBox, bg);
        SetColorInputBoxes(RonekaiColorHexBox, RonekaiColorRgbBox, ronekai);
        SetColorInputBoxes(DenColorHexBox, DenColorRgbBox, den);
        _updatingColorFields = false;
    }

    private static void SetColorInputBoxes(System.Windows.Controls.TextBox hexBox, System.Windows.Controls.TextBox rgbBox, string hex)
    {
        if (UiColorHelper.TryParseHex(hex, out var normalized))
        {
            var (r, g, b) = UiColorHelper.ParseRgb(normalized);
            hexBox.Text = normalized.ToUpperInvariant();
            rgbBox.Text = UiColorHelper.ToRgbString(r, g, b);
        }
    }

    private void ColorInputBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter && sender is System.Windows.Controls.TextBox box)
            ApplyColorFromTextBox(box);
    }

    private void ColorInputBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox box)
            ApplyColorFromTextBox(box);
    }

    private void ApplyColorFromTextBox(System.Windows.Controls.TextBox source)
    {
        if (_updatingColorFields)
            return;

        string raw = source.Text ?? "";
        if (!UiColorHelper.TryParseColorInput(raw, out var hex))
            return;

        ActivateCustomColorsFromSelection();

        var tag = source.Tag as string;
        if (ColorFieldTags.IsMainText(tag))
            _customRonekaiHex = hex;
        else if (ColorFieldTags.IsSuffix(tag))
            _customDenHex = hex;
        else
            _customBackgroundHex = hex;

        ApplyColorPreviews(_customBackgroundHex, _customRonekaiHex, _customDenHex);
        ScheduleLivePreview();
    }

    private BrandColorTheme BuildColorTheme()
    {
        if (ColorPackCombo.SelectedItem is ColorPackListItem item && !item.Theme.IsCustom)
            return item.Theme;

        return BrandColorTheme.CreateCustom(_customBackgroundHex, _customRonekaiHex, _customDenHex);
    }

    private ThemeColorSet BuildThemeColorSet()
    {
        ReadColorAppearanceFromUi();
        var set = _themeColors.Clone();

        if (ColorPackCombo.SelectedItem is ColorPackListItem item && !item.Theme.IsCustom)
        {
            set.SyncPrimaryHexFrom(item.Theme);
            return set;
        }

        set.Background.PrimaryHex = _customBackgroundHex;
        set.MainText.PrimaryHex = _customRonekaiHex;
        set.Suffix.PrimaryHex = _customDenHex;
        if (set.Background.FillMode == ColorFillMode.Solid)
            set.Background.GradientEndHex = _customBackgroundHex;
        if (set.MainText.FillMode == ColorFillMode.Solid)
            set.MainText.GradientEndHex = _customRonekaiHex;
        if (set.Suffix.FillMode == ColorFillMode.Solid)
            set.Suffix.GradientEndHex = _customDenHex;
        return set;
    }

    private void SyncThemeColorHexFromFields(string bg, string ronekai, string den)
    {
        _themeColors.Background.PrimaryHex = bg;
        _themeColors.MainText.PrimaryHex = ronekai;
        _themeColors.Suffix.PrimaryHex = den;
    }

    private void InitializeColorAppearanceUi()
    {
        var fillItems = new object[]
        {
            new AppearanceComboItem("Düz renk", ColorFillMode.Solid),
            new AppearanceComboItem("Gradyan", ColorFillMode.Gradient)
        };
        var dirItems = new object[]
        {
            new AppearanceComboItem("Dikey", GradientDirection.Vertical),
            new AppearanceComboItem("Yatay", GradientDirection.Horizontal),
            new AppearanceComboItem("Çapraz ↘", GradientDirection.DiagonalDown),
            new AppearanceComboItem("Çapraz ↗", GradientDirection.DiagonalUp)
        };

        foreach (var cb in new[] { BackgroundFillModeCombo, MainTextFillModeCombo, SuffixFillModeCombo })
        {
            cb.ItemsSource = fillItems;
            cb.DisplayMemberPath = "Label";
            cb.SelectedValuePath = "Value";
        }

        foreach (var cb in new[] { BackgroundGradientDirectionCombo, MainTextGradientDirectionCombo, SuffixGradientDirectionCombo })
        {
            cb.ItemsSource = dirItems;
            cb.DisplayMemberPath = "Label";
            cb.SelectedValuePath = "Value";
        }

        BindColorAppearanceUi();
    }

    private void BindColorAppearanceUi()
    {
        if (BackgroundFillModeCombo is null || BackgroundOpacitySlider is null)
            return;

        _updatingAppearanceUi = true;
        BindSlotAppearanceUi(BackgroundFillModeCombo, BackgroundOpacitySlider, BackgroundOpacityLabel,
            BackgroundGradientPanel, BackgroundGradientEndHexBox, BackgroundGradientDirectionCombo, _themeColors.Background);
        BindSlotAppearanceUi(MainTextFillModeCombo, MainTextOpacitySlider, MainTextOpacityLabel,
            MainTextGradientPanel, MainTextGradientEndHexBox, MainTextGradientDirectionCombo, _themeColors.MainText);
        BindSlotAppearanceUi(SuffixFillModeCombo, SuffixOpacitySlider, SuffixOpacityLabel,
            SuffixGradientPanel, SuffixGradientEndHexBox, SuffixGradientDirectionCombo, _themeColors.Suffix);
        _updatingAppearanceUi = false;
    }

    private static void BindSlotAppearanceUi(
        ComboBox fillCombo, Slider opacitySlider, TextBlock opacityLabel,
        Grid gradientPanel, TextBox gradientEndBox, ComboBox directionCombo,
        ThemeColorAppearance appearance)
    {
        if (fillCombo.Items.Count == 0)
            return;

        fillCombo.SelectedValue = appearance.FillMode;
        opacitySlider.Value = appearance.Opacity * 100;
        opacityLabel.Text = $"{(int)opacitySlider.Value}%";
        gradientPanel.Visibility = appearance.FillMode == ColorFillMode.Gradient
            ? Visibility.Visible
            : Visibility.Collapsed;
        gradientEndBox.Text = appearance.GradientEndHex.ToUpperInvariant();
        directionCombo.SelectedValue = appearance.GradientDirection;
    }

    private void ReadColorAppearanceFromUi()
    {
        if (_updatingAppearanceUi)
            return;

        if (BackgroundFillModeCombo is null || BackgroundOpacitySlider is null)
            return;

        ReadSlot(BackgroundFillModeCombo, BackgroundOpacitySlider, BackgroundGradientEndHexBox,
            BackgroundGradientDirectionCombo, _themeColors.Background);
        ReadSlot(MainTextFillModeCombo, MainTextOpacitySlider, MainTextGradientEndHexBox,
            MainTextGradientDirectionCombo, _themeColors.MainText);
        ReadSlot(SuffixFillModeCombo, SuffixOpacitySlider, SuffixGradientEndHexBox,
            SuffixGradientDirectionCombo, _themeColors.Suffix);
    }

    private static void ReadSlot(
        ComboBox? fillCombo, Slider? opacitySlider, TextBox? gradientEndBox, ComboBox? directionCombo,
        ThemeColorAppearance appearance)
    {
        if (fillCombo?.SelectedValue is ColorFillMode mode)
            appearance.FillMode = mode;

        if (opacitySlider is not null)
            appearance.Opacity = (float)(opacitySlider.Value / 100.0);

        if (gradientEndBox is not null && UiColorHelper.TryParseHex(gradientEndBox.Text, out var end))
            appearance.GradientEndHex = end;

        if (directionCombo?.SelectedValue is GradientDirection dir)
            appearance.GradientDirection = dir;
    }

    private void ColorAppearance_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingAppearanceUi || _loadingPreset || BackgroundFillModeCombo is null)
            return;
        ActivateCustomColorsFromSelection();
        ReadColorAppearanceFromUi();
        UpdateGradientPanels();
        ApplyColorPreviews(_customBackgroundHex, _customRonekaiHex, _customDenHex);
        ScheduleLivePreview();
    }

    private void ColorAppearanceSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingAppearanceUi || _loadingPreset || BackgroundOpacitySlider is null)
            return;
        ActivateCustomColorsFromSelection();
        ReadColorAppearanceFromUi();
        if (BackgroundOpacityLabel is not null)
            BackgroundOpacityLabel.Text = $"{(int)BackgroundOpacitySlider.Value}%";
        if (MainTextOpacityLabel is not null)
            MainTextOpacityLabel.Text = $"{(int)MainTextOpacitySlider.Value}%";
        if (SuffixOpacityLabel is not null)
            SuffixOpacityLabel.Text = $"{(int)SuffixOpacitySlider.Value}%";
        ApplyColorPreviews(_customBackgroundHex, _customRonekaiHex, _customDenHex);
        ScheduleLivePreview();
    }

    private void GradientEndBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox box)
            return;
        ApplyGradientEndFromBox(box);
    }

    private void GradientEndBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox box)
            ApplyGradientEndFromBox(box);
    }

    private void ApplyGradientEndFromBox(TextBox box)
    {
        if (!UiColorHelper.TryParseColorInput(box.Text, out var hex))
            return;
        ActivateCustomColorsFromSelection();
        var tag = box.Tag as string;
        if (ColorFieldTags.IsMainText(tag))
            _themeColors.MainText.GradientEndHex = hex;
        else if (ColorFieldTags.IsSuffix(tag))
            _themeColors.Suffix.GradientEndHex = hex;
        else
            _themeColors.Background.GradientEndHex = hex;
        box.Text = hex.ToUpperInvariant();
        ApplyColorPreviews(_customBackgroundHex, _customRonekaiHex, _customDenHex);
        ScheduleLivePreview();
    }

    private void UpdateGradientPanels()
    {
        if (BackgroundGradientPanel is not null)
            BackgroundGradientPanel.Visibility = _themeColors.Background.FillMode == ColorFillMode.Gradient
                ? Visibility.Visible : Visibility.Collapsed;
        if (MainTextGradientPanel is not null)
            MainTextGradientPanel.Visibility = _themeColors.MainText.FillMode == ColorFillMode.Gradient
                ? Visibility.Visible : Visibility.Collapsed;
        if (SuffixGradientPanel is not null)
            SuffixGradientPanel.Visibility = _themeColors.Suffix.FillMode == ColorFillMode.Gradient
                ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PickBackgroundColor_Click(object sender, RoutedEventArgs e) =>
        PickCustomColor(ref _customBackgroundHex);

    private void PickRonekaiColor_Click(object sender, RoutedEventArgs e) =>
        PickCustomColor(ref _customRonekaiHex);

    private void PickDenColor_Click(object sender, RoutedEventArgs e) =>
        PickCustomColor(ref _customDenHex);

    private void PickCustomColor(ref string targetHex)
    {
        CancelEyedropper();
        ActivateCustomColorsFromSelection();
        ColorSettingsPanel?.BringIntoView();

        var dialog = new ColorPickerWindow(targetHex, this)
        {
            Title = "Renk seç — PhonixFrame"
        };

        if (dialog.ShowDialog() != true)
            return;

        targetHex = dialog.SelectedHex;
        ApplyColorPreviews(_customBackgroundHex, _customRonekaiHex, _customDenHex);
        ScheduleLivePreview();
    }

    private void RefreshColorFieldLabels()
    {
        if (ColorFieldsHintText is null)
            return;

        string mainSample = string.IsNullOrWhiteSpace(ImageBrandMainBox?.Text)
            ? "ana metin"
            : $"«{ImageBrandMainBox.Text.Trim()}»";
        string suffixSample = string.IsNullOrWhiteSpace(ImageBrandSuffixBox?.Text)
            ? "ek / son ek"
            : $"«{ImageBrandSuffixBox.Text.Trim()}»";

        if (BackgroundColorLabel is not null)
            BackgroundColorLabel.Text = "Zemin";
        if (MainTextColorLabel is not null)
            MainTextColorLabel.Text = "Ana metin";
        if (SuffixColorLabel is not null)
            SuffixColorLabel.Text = "Ek metin";

        ColorFieldsHintText.Text =
            $"Zemin = arka plan rengi · Ana metin = görseldeki {mainSample} yazısı · Ek metin = {suffixSample} yazısı. " +
            "Marka şeritleri ana metin rengini kullanır.";
    }

    private string BuildColorFieldsHint() =>
        "Hex veya RGB yazın; Seç… / Damla ile renk alın. «Opaklık ve gradyan» ile yoğunluk ve gradyan ayarlayın.";

    private void EyedropColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag })
            return;

        if (LivePreviewImage.Source is not BitmapSource || LivePreviewImage.Visibility != Visibility.Visible)
        {
            MessageBox.Show(
                "Önizleme görseli hazır değil. Önce bir şablon ve (isteğe bağlı) kaynak klasör seçin.",
                "Damla aracı",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        StartEyedropper(tag);
    }

    private void StartEyedropper(string fieldTag)
    {
        CancelEyedropper();
        _eyedropperColorField = fieldTag;
        ActivateCustomColorsFromSelection();
        ColorSettingsPanel?.BringIntoView();

        if (EyedropperHintText is not null)
        {
            string target = fieldTag switch
            {
                var t when ColorFieldTags.IsBrandLogoTintEnd(t) => "marka logo gradyan sonu",
                var t when ColorFieldTags.IsBrandLogoTint(t) => "marka logo rengi",
                var t when ColorFieldTags.IsMainText(t) => "ana metin",
                var t when ColorFieldTags.IsSuffix(t) => "ek metin",
                _ => "zemin"
            };
            EyedropperHintText.Text =
                $"Damla aktif: önizleme görselinde {target} rengi için bir piksele tıklayın (Esc = iptal).";
            EyedropperHintText.Visibility = Visibility.Visible;
        }

        LivePreviewImage.Cursor = Cursors.Cross;
        Mouse.OverrideCursor = Cursors.Cross;
    }

    private void CancelEyedropper()
    {
        _eyedropperColorField = null;
        if (EyedropperHintText is not null)
            EyedropperHintText.Visibility = Visibility.Collapsed;
        LivePreviewImage.Cursor = Cursors.Arrow;
        Mouse.OverrideCursor = null;
    }

    private void LivePreviewImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_eyedropperColorField is null)
            return;

        if (LivePreviewImage.Source is not BitmapSource bitmap)
            return;

        var pos = e.GetPosition(LivePreviewImage);
        if (!PreviewColorSampler.TryPick(bitmap, LivePreviewImage, pos, out var hex))
        {
            MessageBox.Show(
                "Renk alınamadı. Görselin üzerindeki bir noktaya tıklayın.",
                "Damla aracı",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        ApplyEyedropperColor(_eyedropperColorField, hex);
        CancelEyedropper();
        e.Handled = true;
    }

    private void ApplyEyedropperColor(string fieldTag, string hex)
    {
        if (ColorFieldTags.IsBrandLogoTintEnd(fieldTag))
        {
            _brandLogoTint.GradientEndHex = hex;
            BindBrandLogoTintUi();
            BrandLogoRenderer.ClearCache();
            PersistImageBrandSettings();
            ScheduleLivePreview();
            return;
        }

        if (ColorFieldTags.IsBrandLogoTint(fieldTag))
        {
            _brandLogoTint.PrimaryHex = hex;
            BindBrandLogoTintUi();
            BrandLogoRenderer.ClearCache();
            PersistImageBrandSettings();
            ScheduleLivePreview();
            return;
        }

        ActivateCustomColorsFromSelection();

        if (ColorFieldTags.IsMainText(fieldTag))
            _customRonekaiHex = hex;
        else if (ColorFieldTags.IsSuffix(fieldTag))
            _customDenHex = hex;
        else
            _customBackgroundHex = hex;

        ApplyColorPreviews(_customBackgroundHex, _customRonekaiHex, _customDenHex);
        ScheduleLivePreview();
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _eyedropperColorField is not null)
        {
            CancelEyedropper();
            e.Handled = true;
        }
    }

    private void SelectCustomColorPack()
    {
        var custom = ColorPackRegistry.GetCustomItem();
        if (custom is null) return;
        ColorPackCombo.SelectedItem = custom;
    }

    private void LogoModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LogoModeCombo.SelectedItem is LogoModeListItem item
            && item.Mode != LogoOverlayMode.None
            && !_loadingPreset)
        {
            _updatingOpacity = true;
            LogoOpacitySlider.Value = LogoOverlaySettings.DefaultOpacity(item.Mode) * 100;
            _updatingOpacity = false;
        }

        RefreshLogoModeUi();
        ScheduleLivePreview();
    }

    private void RefreshLogoModeUi()
    {
        if (LogoModeCombo.SelectedItem is not LogoModeListItem item)
            return;

        LogoModeDescription.Text = item.Description;
        bool usesLogo = item.Mode != LogoOverlayMode.None;
        bool filigran = item.Mode == LogoOverlayMode.Filigran;
        LogoOpacitySlider.IsEnabled = usesLogo;
        if (FiligranOptionsPanel is not null)
            FiligranOptionsPanel.Visibility = filigran ? Visibility.Visible : Visibility.Collapsed;
        if (LogoPlacementCombo is not null)
            LogoPlacementCombo.IsEnabled = usesLogo && filigran;
        if (LogoScaleSlider is not null)
            LogoScaleSlider.IsEnabled = usesLogo && filigran;
        RefreshLogoPathUi();
        RefreshLogoOverlayLabels();
    }

    private void LogoOverlayOption_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingPreset)
            return;
        RefreshLogoOverlayLabels();
        ScheduleLivePreview();
    }

    private void RefreshLogoOverlayLabels()
    {
        if (LogoScaleLabel is not null && LogoScaleSlider is not null)
            LogoScaleLabel.Text = $"{(int)LogoScaleSlider.Value}%";
        UpdateOpacityLabel();
    }

    private void LogoOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingOpacity || LogoOpacityLabel is null) return;
        UpdateOpacityLabel();
        ScheduleLivePreview();
    }

    private void UpdateOpacityLabel()
    {
        if (LogoOpacityLabel is null) return;
        LogoOpacityLabel.Text = $"{(int)LogoOpacitySlider.Value}%";
    }

    private void SourceFolderBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var newPath = GetRootSourceFolder();
        if (!_loadingPreset
            && !string.IsNullOrEmpty(_lastSourceFolderPath)
            && !string.Equals(_lastSourceFolderPath, newPath, StringComparison.OrdinalIgnoreCase))
        {
            PersistActiveFolderLogoSettings();
        }

        RefreshSubfolderCombo(newPath);
        _brandLogoEditingFilePath = null;
        SourceFileList.SelectedItems.Clear();
        RefreshImageCount();
        RefreshSourceFileList();
        UpdateOutputPreview();
        BrandLogoRenderer.ClearCache();
        if (!_loadingPreset)
            LoadBrandLogoUiForCurrentScope();
        ScheduleLivePreview();
        _lastSourceFolderPath = newPath;
        _lastActiveSourceFolder = GetActiveSourceFolder();
    }

    private void BrowseSource_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Ürün fotoğraflarının bulunduğu klasörü seçin",
            InitialDirectory = string.IsNullOrWhiteSpace(SourceFolderBox.Text)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
                : SourceFolderBox.Text
        };

        if (dialog.ShowDialog() == true)
        {
            SourceFolderBox.Text = dialog.FolderName;
            RefreshImageCount();
            UpdateOutputPreview();
        }
    }

    private void BrowseLogo_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "RONEKAI logosu seçin",
            Filter = LogoImageLoader.OpenFileDialogFilter,
            InitialDirectory = Directory.Exists(LogoProvider.AssetsFolder)
                ? LogoProvider.AssetsFolder
                : AppPaths.ProgramRoot
        };

        if (dialog.ShowDialog() == true)
        {
            _customLogoPath = dialog.FileName;
            _updatingLogoPathUi = true;
            UseDefaultLogoCheck.IsChecked = false;
            _updatingLogoPathUi = false;
            LogoPathBox.Text = _customLogoPath;
            LogoProvider.ClearCache();
            PersistLogoPathSettings();
            RefreshLogoPathUi();
            ScheduleLivePreview();
        }
    }

    private void ScheduleLivePreview()
    {
        if (!_previewReady)
            return;

        _previewCts?.Cancel();
        _previewCts?.Dispose();
        _previewCts = new CancellationTokenSource();
        var token = _previewCts.Token;
        _ = RefreshLivePreviewAsync(token);
    }

    private async Task RefreshLivePreviewAsync(CancellationToken ct)
    {
        LivePreviewResult result;
        try
        {
            try
            {
                await Task.Delay(180, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            var theme = BuildColorTheme();
            var themeColors = BuildThemeColorSet();
            var logo = BuildLogoSettings();
            var imageBrand = BuildPreviewImageBrandSettings();
            var exportProfile = GetSelectedExportProfile();
            var job = BuildJobSettings();
            string? sampleFile = TryGetPreviewSourceImageFile(GetActiveSourceFolder());
            var template = ResolvePreviewTemplate();

            result = await Task.Run(
                () => TemplatePreviewService.Render(
                    template, theme, themeColors, logo, imageBrand, exportProfile, job, sampleFile),
                ct);

            if (ct.IsCancellationRequested)
                return;
        }
        catch (Exception ex)
        {
            result = new LivePreviewResult(null, "", "Önizleme hatası", false, ex.Message);
        }

        await Dispatcher.InvokeAsync(() => ApplyLivePreviewResult(result), System.Windows.Threading.DispatcherPriority.Normal);
    }

    private IProductTemplate ResolvePreviewTemplate()
    {
        if (TemplateCombo.SelectedItem is TemplateListItem item)
            return item.Template;

        return TemplateRegistry.GetById("white-studio")
               ?? TemplateRegistry.Templates.First(t => !t.IsPassthrough);
    }

    private void ApplyLivePreviewResult(LivePreviewResult result)
    {
        if (result.Success && result.PreviewPng is { Length: > 0 })
        {
            try
            {
                LivePreviewImage.Source = WpfImageHelper.FromPngBytes(result.PreviewPng);
            }
            catch (Exception ex)
            {
                result = result with { Success = false, ErrorMessage = ex.Message };
                LivePreviewImage.Source = null;
                LivePreviewImage.Visibility = Visibility.Collapsed;
                PreviewPlaceholderText.Visibility = Visibility.Visible;
                PreviewPlaceholderText.Text = "Önizleme gösterilemedi";
                PreviewErrorText.Text = ex.Message;
                PreviewErrorText.Visibility = Visibility.Visible;
                return;
            }
            LivePreviewImage.Visibility = Visibility.Visible;
            PreviewPlaceholderText.Visibility = Visibility.Collapsed;
            PreviewErrorText.Visibility = Visibility.Collapsed;
        }
        else
        {
            LivePreviewImage.Source = null;
            LivePreviewImage.Visibility = Visibility.Collapsed;
            PreviewPlaceholderText.Visibility = Visibility.Visible;
            PreviewPlaceholderText.Text = result.ErrorMessage is not null
                ? "Önizleme oluşturulamadı"
                : "Önizleme yok";
            if (result.ErrorMessage is not null)
            {
                PreviewErrorText.Text = result.ErrorMessage;
                PreviewErrorText.Visibility = Visibility.Visible;
            }
            else
            {
                PreviewErrorText.Visibility = Visibility.Collapsed;
            }
        }

        PreviewSizeText.Text = string.IsNullOrEmpty(result.SizeLabel) ? "" : result.SizeLabel;
        PreviewCaptionText.Text = result.Caption;

        bool realPhoto = result.Caption.Contains("Gerçek fotoğraf", StringComparison.Ordinal);
        PreviewModeBadgeText.Text = realPhoto ? "CANLI" : "DEMO";
        PreviewModeBadge.Background = realPhoto
            ? UiColorHelper.ToSolidBrush("#1B7F6E")
            : UiColorHelper.ToSolidBrush("#1B2A4A");
    }

    private string? TryGetPreviewSourceImageFile(string? folder)
    {
        if (SourceFileList.SelectedItems.Count > 0)
        {
            foreach (var item in SourceFileList.SelectedItems)
            {
                if (item is string path && File.Exists(path))
                    return path;
            }
        }

        return TryGetFirstSourceImageFile(folder);
    }

    private static string? TryGetFirstSourceImageFile(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return null;
        try
        {
            return BatchProcessor.FindImages(folder).FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static void TryGetFirstSourceImageSize(string? folder, out int? width, out int? height)
    {
        width = null;
        height = null;
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return;

        try
        {
            var first = BatchProcessor.FindImages(folder).FirstOrDefault();
            if (first is null)
                return;

            using var img = SourceImageLoader.Load(first);
            width = img.Width;
            height = img.Height;
        }
        catch
        {
            // önizleme demo boyutuyla devam eder
        }
    }

    private void RefreshImageCount()
    {
        var path = GetActiveSourceFolder();
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            ImageCountText.Text = "";
            return;
        }

        try
        {
            var files = BatchProcessor.FindImages(path!);
            int count = files.Count;
            int heif = BatchProcessor.CountHeifImages(files);
            ImageCountText.Text = count > 0
                ? heif > 0
                    ? $"{count} resim ({heif} HEIC)"
                    : $"{count} resim"
                : "Resim yok — .jpg, .heic veya .hdc dosyalarını bu klasöre koyun";
        }
        catch
        {
            ImageCountText.Text = "";
        }
    }

    private void UpdateOutputPreview()
    {
        var root = GetRootSourceFolder();
        var active = GetActiveSourceFolder();
        OutputPathText.Text = AppPaths.PreviewOutputPath(root, active);
    }

    private void PersistActiveFolderLogoSettings()
    {
        if (!string.IsNullOrEmpty(_lastActiveSourceFolder))
            PersistBrandLogoForFolder(_lastActiveSourceFolder);
    }

    private void SyncGlobalBrandLogoStore(ImageBrandSettings logoSettings)
    {
        var current = ImageBrandStore.Load();
        current.ShowBrandLogo = logoSettings.ShowBrandLogo;
        current.BrandLogoPresetId = logoSettings.BrandLogoPresetId;
        current.BrandLogoPath = logoSettings.BrandLogoPath;
        current.BrandLogoSizePercent = logoSettings.BrandLogoSizePercent;
        current.BrandLogoOpacity = logoSettings.BrandLogoOpacity;
        current.BrandLogoPlacement = logoSettings.BrandLogoPlacement;
        current.BrandLogoOffsetX = logoSettings.BrandLogoOffsetX;
        current.BrandLogoOffsetY = logoSettings.BrandLogoOffsetY;
        current.BrandLogoTintEnabled = logoSettings.BrandLogoTintEnabled;
        current.BrandLogoTint = logoSettings.BrandLogoTint.Clone();
        ImageBrandStore.Save(current);
    }

    private LogoOverlaySettings BuildLogoSettings()
    {
        var mode = LogoModeCombo.SelectedItem is LogoModeListItem item
            ? item.Mode
            : LogoOverlayMode.None;

        return new LogoOverlaySettings
        {
            Mode = mode,
            Opacity = (float)(LogoOpacitySlider.Value / 100.0),
            LogoFilePath = ResolveActiveLogoPath(),
            Placement = LogoPlacementCombo.SelectedItem is PlacementListItem p
                ? p.Placement
                : OverlayPlacement.Center,
            ScalePercent = LogoScaleSlider is null ? 62 : (int)LogoScaleSlider.Value
        };
    }

    private async void Process_Click(object sender, RoutedEventArgs e)
    {
        if (TemplateCombo.SelectedItem is not TemplateListItem selected)
        {
            MessageBox.Show("Lütfen bir şablon seçin.", "PhonixFrame",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var source = GetActiveSourceFolder();
        var root = GetRootSourceFolder();
        if (string.IsNullOrEmpty(source) || !Directory.Exists(source)
            || string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            MessageBox.Show("Geçerli bir kaynak klasör seçin.", "PhonixFrame",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var allImages = BatchProcessor.FindImages(source!);
        if (allImages.Count == 0)
        {
            MessageBox.Show(
                $"Klasörde desteklenen görsel yok.\n\n{ImageInputCatalog.SupportedFormatsDescription}",
                "PhonixFrame",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var job = BuildJobSettings();
        IReadOnlyList<string> images = allImages;
        if (job.ProcessOnlySelectedFiles)
        {
            images = SourceFileList.SelectedItems.Cast<string>().ToList();
            if (images.Count == 0)
            {
                MessageBox.Show("Dosya listesinden en az bir dosya seçin veya «Seçili dosyaları işle» işaretini kaldırın.",
                    "PhonixFrame", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        PersistBrandLogoFromUi(
            IsPerFileBrandLogoMode() ? _brandLogoEditingFilePath : null);
        var colorTheme = BuildColorTheme();
        var logoSettings = BuildLogoSettings();
        var imageBrand = BuildImageBrandSettings();
        var folderLogoSettings = GetCurrentFolderLogoSettings();
        var exportProfile = GetSelectedExportProfile();
        if (logoSettings.UsesLogo)
        {
            var logoPath = LogoProvider.ResolveLogoPath(logoSettings.LogoFilePath);
            if (logoPath is null)
            {
                string hint = UsesDefaultLogo()
                    ? $"Varsayılan logo bulunamadı. Dosyayı şuraya koyun:\n{LogoProvider.DefaultLogoPath}"
                    : $"Logo dosyası bulunamadı.\n\n'Logo seç…' ile dosya gösterin veya 'Varsayılan logo' seçeneğini işaretleyin.\n\nVarsayılan konum:\n{LogoProvider.DefaultLogoPath}";

                MessageBox.Show(hint, "PhonixFrame", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            RefreshLogoPathUi();
            logoSettings = logoSettings with { LogoFilePath = ResolveActiveLogoPath() ?? logoPath };
            UpdateLogoFormatLabel(logoSettings.LogoFilePath ?? logoPath);
        }

        SetBusy(true);
        _log.Clear();
        ProgressBar.Value = 0;
        StatusText.Text = "Hazırlanıyor…";

        _cts = new CancellationTokenSource();
        string outputFolder = AppPaths.CreateOutputFolder(root!, source);
        _lastOutputFolder = outputFolder;
        OutputPathText.Text = outputFolder;

        var progress = new Progress<ProcessProgress>(p =>
        {
            ProgressBar.Value = p.Total > 0 ? (double)p.Current / p.Total * 100 : 0;
            StatusText.Text = $"İşleniyor {p.Current} / {p.Total}";
            _log.Add(p.Message);
            if (LogList.Items.Count > 0)
                LogList.ScrollIntoView(LogList.Items[^1]);
        });

        try
        {
            var themeColors = BuildThemeColorSet();
            var result = await BatchProcessor.ProcessFilesAsync(
                images,
                outputFolder,
                selected.Template,
                colorTheme,
                themeColors,
                logoSettings,
                imageBrand,
                exportProfile,
                job,
                folderLogoSettings,
                progress,
                _cts.Token);

            StatusText.Text = $"Tamamlandı — {result.Success} başarılı, {result.Failed} hata";
            ProgressBar.Value = 100;

            if (result.Success > 0)
                OpenOutputButton.IsEnabled = true;

            ShowProcessSummary(result);
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "İşlem iptal edildi.";
            MessageBox.Show("İşlem iptal edildi.", "PhonixFrame",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Hata oluştu.";
            MessageBox.Show(ex.Message, "PhonixFrame",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async void ProcessAllFolders_Click(object sender, RoutedEventArgs e)
    {
        if (TemplateCombo.SelectedItem is not TemplateListItem selected)
        {
            MessageBox.Show("Lütfen bir şablon seçin.", "PhonixFrame",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var root = GetRootSourceFolder();
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            MessageBox.Show("Geçerli bir ana klasör seçin.", "PhonixFrame",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var folders = new List<string> { BrandLogoResolver.NormalizePath(root) };
        folders.AddRange(GetImmediateSubfolders(root).Select(BrandLogoResolver.NormalizePath));

        var baseJob = BuildJobSettings();
        var job = new ProcessingJobSettings
        {
            ResizeOnly = baseJob.ResizeOnly,
            StretchToExport = baseJob.StretchToExport,
            ResponsiveProductFit = baseJob.ResponsiveProductFit,
            JpegQuality = baseJob.JpegQuality,
            SaveAsPng = baseJob.SaveAsPng,
            FileNamePattern = baseJob.FileNamePattern,
            TextOverlay = baseJob.TextOverlay,
            SamplePreviewCount = baseJob.SamplePreviewCount,
            ProcessOnlySelectedFiles = false
        };
        var colorTheme = BuildColorTheme();
        var themeColors = BuildThemeColorSet();
        var logoSettings = BuildLogoSettings();
        var imageBrand = BuildImageBrandSettings();
        var exportProfile = GetSelectedExportProfile();

        if (logoSettings.UsesLogo)
        {
            var logoPath = LogoProvider.ResolveLogoPath(logoSettings.LogoFilePath);
            if (logoPath is null)
            {
                string hint = UsesDefaultLogo()
                    ? $"Varsayılan logo bulunamadı. Dosyayı şuraya koyun:\n{LogoProvider.DefaultLogoPath}"
                    : $"Logo dosyası bulunamadı.\n\n'Logo seç…' ile dosya gösterin veya 'Varsayılan logo' seçeneğini işaretleyin.\n\nVarsayılan konum:\n{LogoProvider.DefaultLogoPath}";

                MessageBox.Show(hint, "PhonixFrame", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            RefreshLogoPathUi();
            logoSettings = logoSettings with { LogoFilePath = ResolveActiveLogoPath() ?? logoPath };
            UpdateLogoFormatLabel(logoSettings.LogoFilePath ?? logoPath);
        }

        SetBusy(true);
        _log.Clear();
        ProgressBar.Value = 0;
        StatusText.Text = "Hazırlanıyor…";

        _cts = new CancellationTokenSource();
        string outputRoot = AppPaths.CreateOutputFolder(root, root, nestSourceFolder: false);
        _lastOutputFolder = outputRoot;
        OutputPathText.Text = outputRoot;

        int totalFiles = 0;
        var plan = new List<(string Folder, IReadOnlyList<string> Files, SourceFolderLogoSettings? FolderLogo)>();
        foreach (var f in folders)
        {
            var files = BatchProcessor.FindImages(f);
            if (files.Count == 0)
                continue;
            totalFiles += files.Count;
            plan.Add((f, files, GetFolderLogoSettings(f)));
        }

        if (plan.Count == 0)
        {
            SetBusy(false);
            _cts?.Dispose();
            _cts = null;
            MessageBox.Show(
                $"Klasörlerde desteklenen görsel yok.\n\n{ImageInputCatalog.SupportedFormatsDescription}",
                "PhonixFrame",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        int done = 0;
        var progress = new Progress<ProcessProgress>(p =>
        {
            // Toplamı klasörler arası birleştiriyoruz: p.Current tek klasör içindir.
            // Burada "done + p.Current" yaklaşımı yeterli.
            ProgressBar.Value = totalFiles > 0 ? (double)(done + p.Current) / totalFiles * 100 : 0;
            StatusText.Text = $"İşleniyor {done + p.Current} / {totalFiles}";
            _log.Add(p.Message);
            if (LogList.Items.Count > 0)
                LogList.ScrollIntoView(LogList.Items[^1]);
        });

        try
        {
            int success = 0;
            int failed = 0;
            int heif = 0;
            var mergedLog = new List<string>();

            foreach (var item in plan)
            {
                _cts.Token.ThrowIfCancellationRequested();
                var relative = AppPaths.ResolveRelativeOutputPath(root, item.Folder);
                var outFolder = Path.Combine(outputRoot, relative);
                Directory.CreateDirectory(outFolder);

                var result = await BatchProcessor.ProcessFilesAsync(
                    item.Files,
                    outFolder,
                    selected.Template,
                    colorTheme,
                    themeColors,
                    logoSettings,
                    imageBrand,
                    exportProfile,
                    job,
                    item.FolderLogo,
                    progress,
                    _cts.Token);

                success += result.Success;
                failed += result.Failed;
                heif += result.HeifInBatch;
                var logLabel = relative;
                foreach (var line in result.Log)
                    mergedLog.Add($"[{logLabel}] {line}");

                done += item.Files.Count;
            }

            StatusText.Text = $"Tamamlandı — {success} başarılı, {failed} hata";
            ProgressBar.Value = 100;
            if (success > 0)
                OpenOutputButton.IsEnabled = true;

            ShowProcessSummary(new ProcessResult(success, failed, heif, outputRoot, null, mergedLog));
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "İşlem iptal edildi.";
            MessageBox.Show("İşlem iptal edildi.", "PhonixFrame",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Hata oluştu.";
            MessageBox.Show(ex.Message, "PhonixFrame",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    private static void ShowProcessSummary(ProcessResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Başarılı: {result.Success}");
        sb.AppendLine($"Hatalı: {result.Failed}");
        if (result.HeifInBatch > 0)
            sb.AppendLine($"Kaynakta HEIC/HEIF: {result.HeifInBatch}");
        sb.AppendLine();
        sb.AppendLine($"Çıktı klasörü:\n{result.OutputFolder}");

        var errors = result.Log.Where(l => l.StartsWith('✗')).Take(8).ToList();
        if (errors.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Hata detayları:");
            foreach (var line in errors)
                sb.AppendLine(line);
            if (result.Failed > errors.Count)
                sb.AppendLine($"... ve {result.Failed - errors.Count} hata daha (günlükte)");
        }

        MessageBoxImage icon = result.Success switch
        {
            > 0 when result.Failed > 0 => MessageBoxImage.Warning,
            > 0 => MessageBoxImage.Information,
            _ => MessageBoxImage.Error
        };

        string title = result.Success > 0
            ? (result.Failed > 0 ? "Tamamlandı (bazı dosyalar hatalı)" : "Tamamlandı")
            : "İşlem başarısız";

        MessageBox.Show(sb.ToString().TrimEnd(), title,
            MessageBoxButton.OK, icon);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private void OpenOutput_Click(object sender, RoutedEventArgs e)
    {
        var path = _lastOutputFolder;
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            var source = SourceFolderBox.Text?.Trim();
            if (!string.IsNullOrEmpty(source) && Directory.Exists(source))
                path = source;
            else
            {
                MessageBox.Show("Henüz çıktı klasörü yok. Önce resimleri işleyin.", "PhonixFrame",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
        }
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Klasör açılamadı: {ex.Message}", "PhonixFrame",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SetBusy(bool busy)
    {
        ProcessButton.IsEnabled = !busy;
        if (ProcessAllFoldersButton is not null)
            ProcessAllFoldersButton.IsEnabled = !busy;
        CancelButton.IsEnabled = busy;
        TemplateCombo.IsEnabled = !busy;
        ColorPackCombo.IsEnabled = !busy;
        PickBackgroundColorButton.IsEnabled = !busy;
        PickRonekaiColorButton.IsEnabled = !busy;
        PickDenColorButton.IsEnabled = !busy;
        LogoModeCombo.IsEnabled = !busy;
        UseDefaultLogoCheck.IsEnabled = !busy;
        SourceFolderBox.IsEnabled = !busy;
        if (SourceSubfolderCombo is not null)
            SourceSubfolderCombo.IsEnabled = !busy && (SourceSubfolderCombo.Items.Count > 1);
        ExportResolutionCombo.IsEnabled = !busy;
        ImageBrandMainBox.IsEnabled = !busy;
        ImageBrandSuffixBox.IsEnabled = !busy;
        ShowImageBrandMainCheck.IsEnabled = !busy;
        ShowImageBrandSuffixCheck.IsEnabled = !busy;
        ShowBrandLogoCheck.IsEnabled = !busy;
        if (!busy)
        {
            RefreshImageBrandSizeUi();
            RefreshBrandLogoUi();
        }
        else
        {
            ImageBrandMainSizeSlider.IsEnabled = false;
            ImageBrandSuffixSizeSlider.IsEnabled = false;
            BrandLogoPathBox.IsEnabled = false;
            BrandLogoPlacementCombo.IsEnabled = false;
            BrandLogoSizeSlider.IsEnabled = false;
            BrandLogoOpacitySlider.IsEnabled = false;
            BrandLogoOffsetXSlider.IsEnabled = false;
            BrandLogoOffsetYSlider.IsEnabled = false;
            BrandLogoTintEnabledCheck.IsEnabled = false;
            if (BrandLogoTintPanel is not null)
                BrandLogoTintPanel.IsEnabled = false;
            LogoPlacementCombo.IsEnabled = false;
            LogoScaleSlider.IsEnabled = false;
        }
        ResizeOnlyCheck.IsEnabled = !busy;
        JpegQualitySlider.IsEnabled = !busy;
        SaveAsPngCheck.IsEnabled = !busy;
        OutputFileNameBox.IsEnabled = !busy;
        TextOverlayEnabledCheck.IsEnabled = !busy;
        TextOverlayTextBox.IsEnabled = !busy && TextOverlayEnabledCheck.IsChecked == true;
        SourceFileList.IsEnabled = !busy;
        ProcessSelectedOnlyCheck.IsEnabled = !busy;
        PresetCombo.IsEnabled = !busy;
        PresetNameBox.IsEnabled = !busy;
        SavePresetButton.IsEnabled = !busy;
        BrandMainFontCombo.IsEnabled = !busy;
        BrandSuffixFontCombo.IsEnabled = !busy;
        FavoriteTemplateButton.IsEnabled = !busy;
        ProcessButton.Content = busy ? "İşleniyor…" : "Bu klasörü işle";

        if (!busy)
            RefreshLogoModeUi();
    }
}
