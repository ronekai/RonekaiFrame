using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ellipse = System.Windows.Shapes.Ellipse;
using Polygon = System.Windows.Shapes.Polygon;
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
    private int _previewGeneration;
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

    // Canlı önizlemede kırpma: pending seçim + uygulanan crop
    private NormalizedCropRect? _activeCropRect;
    private NormalizedCropRect? _pendingCropRect;
    private readonly Stack<NormalizedCropRect?> _cropUndoStack = [];
    private bool _isCropping;
    private Point _cropDragStart;
    private NormalizedCropRect? _cropRectAtDragStart;
    private string _cropDragMode = "none"; // none | create | move | resize
    private string _cropResizeHandle = ""; // nw,ne,sw,se,n,s,e,w
    private int _previewPixelWidth;
    private int _previewPixelHeight;
    private bool _updatingCropPxUi;
    private readonly List<WatermarkCleanOp> _watermarkCleanOps = [];
    private readonly List<TextureCloneOp> _textureCloneOps = [];
    private readonly List<SelectionPasteOp> _selectionPasteOps = [];
    private readonly Dictionary<string, PerFilePreviewEditState> _perFilePreviewEdits =
        new(StringComparer.OrdinalIgnoreCase);

    // Şekil seçim kopyala / yapıştır (döndürmeli)
    private byte[]? _copiedSelectionPng;
    private TextureCloneBrushShape _copiedSelectionShape = TextureCloneBrushShape.Square;
    private int _copiedPatchPixelW;
    private int _copiedPatchPixelH;
    private bool _floatingPasteActive;
    private Point _floatingPasteCenterCanvas; // 0..1 tuval
    private double _floatingPasteRotationDeg;
    private bool _floatingPasteDragging;
    private Point _floatingPasteDragStart;
    private Point _floatingPasteCenterAtDragStart;
    private bool _cloneStampMode;
    private bool _filigramBrushMode;
    private Point? _filigramBrushCenterCanvas; // tuval norm — şekil+boyut seçimi
    private Point? _filigramHoverNorm;
    private bool _clonePickSourceNext;
    private Point? _cloneSourceNorm; // sabit kaynak merkezi (önizleme / şablon tuvali, normalize 0..1)
    private Point? _cloneHoverNorm;
    private bool _clonePainting;
    private Point? _cloneLastStampNorm;
    private long _lastCloneOverlayTick;
    /// <summary>Kenar uzatma için isteğe bağlı örnek şerit (tuval normalize).</summary>
    private NormalizedCropRect? _edgePadSampleRect;
    private double _previewZoom = 1.0;
    private const double PreviewZoomMin = 0.5;
    private const double PreviewZoomMax = 8.0;
    private const double PreviewZoomStep = 1.15;
    private bool _updatingZoomHost;
    private bool _pinSelectMode;
    private readonly List<Point> _selectionPins = []; // normalized 0..1
    private const int MaxSelectionPins = 20;
    private bool _pinDragging;
    private List<Point>? _pinsAtCropDragStart;
    private bool _updatingBrushFromPin;
    private SelectionSnapshot? _lastClearedSelection;

    private sealed class SelectionSnapshot
    {
        public NormalizedCropRect? Pending { get; init; }
        public List<Point> Pins { get; init; } = [];
        public Point? FiligramCenter { get; init; }
        public double? BrushSizePct { get; init; }
    }

    private bool _updatingAppearanceUi;
    private bool _updatingBrandLogoUi;
    private string? _brandLogoEditingFilePath;
    private string? _livePreviewSourceFile;
    private string? _lastPreviewSourceForCropReset;
    private bool _suppressSourceSelectionHandler;
    private string? _lastSourceFolderPath;
    private string? _lastActiveSourceFolder;
    private bool _updatingSourceFolderUi;

    private sealed record FolderListItem(string Label, string Path);
    private sealed record OutputFormatListItem(string Label, bool SaveAsPng);

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

        RefreshTemplateComboItems(selectId: "sablon-yok");
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
        InitializeOutputFormatCombo();
        LoadPresets();
        // Açılışta varsayılan şablon: Şablon yok
        RefreshTemplateComboItems(selectId: "sablon-yok");

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
        InitializeFiligramCleanStyleCombo();
        InitializeCloneBrushShapeCombo();
        RefreshPreviewZoomUi();
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
        // Klasörün tamamı: UI kaynak — eski PerFile kayıtları paradox yaratmasın
        if (!IsPerFileBrandLogoMode())
            return global;

        string? sampleFile = TryGetPreviewSourceImageFile(GetActiveSourceFolder());
        if (sampleFile is null)
            return global;

        return BrandLogoResolver.ResolveForFile(
            sampleFile,
            global,
            GetCurrentFolderLogoSettings(),
            preferPerFileOverrides: true);
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
        _livePreviewSourceFile = null;
        _lastPreviewSourceForCropReset = null;
        ClearAllPerFilePreviewEdits();
        PreviewSourceCache.Invalidate();
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

        // Önizleme odağı ile logo düzenleme aynı dosyayı göstersin
        if (!string.IsNullOrEmpty(_livePreviewSourceFile)
            && IsFileInSourceFolder(_livePreviewSourceFile, folder)
            && IsSourcePathSelected(_livePreviewSourceFile))
        {
            return _livePreviewSourceFile;
        }

        if (SourceFileList.SelectedItems.Count == 1
            && SourceFileList.SelectedItems[0] is string single
            && IsFileInSourceFolder(single, folder))
        {
            return single;
        }

        if (!string.IsNullOrEmpty(_brandLogoEditingFilePath)
            && IsFileInSourceFolder(_brandLogoEditingFilePath, folder)
            && IsSourcePathSelected(_brandLogoEditingFilePath))
        {
            return _brandLogoEditingFilePath;
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
            ? "İşlem: yalnızca seçili dosyalar. Önizleme: son tıkladığınız dosya. Logo/renk/sığdırma bu önizlemeye uygulanır."
            : "İşlem: klasörün tamamı. Önizleme: listeden tıkladığınız dosya (yoksa ilk görsel). Ayarlar tüm çıktılara uygulanır.";
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
        ImageBrandStore.Save(logoSettings);

        var folderSettings = GetCurrentFolderLogoSettings();
        if (folderSettings is not null)
        {
            folderSettings.FolderDefault = ReadBrandLogoUiAsOverride();
            PrunePerFileEntries(folderSettings, folderSettings.FolderPath!);
            SourceFolderLogoStore.Save(folderSettings);
        }
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
            BrandLogoScopeHint.Text = "Logo ve konum bu klasördeki TÜM görsellere uygulanır. «Seçili dosyaları işle» kapalıyken dosya-başı logo kayıtları kullanılmaz.";
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

            if (!string.IsNullOrEmpty(_livePreviewSourceFile))
                _brandLogoEditingFilePath = _livePreviewSourceFile;
            LoadBrandLogoUiForCurrentScope();
        }
        else
        {
            RefreshBrandLogoScopeHint();
        }

        // Yalnızca önizlenen dosya değişince düzenlemeleri kaydet / geri yükle
        var previewPath = TryGetPreviewSourceImageFile(GetActiveSourceFolder());
        if (!string.Equals(_lastPreviewSourceForCropReset, previewPath, StringComparison.OrdinalIgnoreCase))
        {
            PersistPreviewEditsForFile(_lastPreviewSourceForCropReset);
            _lastPreviewSourceForCropReset = previewPath;
            LoadPreviewEditsForFile(previewPath);
        }

        ScheduleLivePreview();
    }

    private sealed class PerFilePreviewEditState
    {
        public List<WatermarkCleanOp> CleanOps { get; init; } = [];
        public List<TextureCloneOp> CloneOps { get; init; } = [];
        public List<SelectionPasteOp> PasteOps { get; init; } = [];
        public NormalizedCropRect? ActiveCrop { get; init; }
        public NormalizedCropRect? PendingCrop { get; init; }
    }

    private static string NormalizeEditFileKey(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }

    private void PersistPreviewEditsForFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        string key = NormalizeEditFileKey(path);
        bool hasWork = _watermarkCleanOps.Count > 0
                       || _textureCloneOps.Count > 0
                       || _selectionPasteOps.Count > 0
                       || _activeCropRect is not null
                       || _pendingCropRect is not null;
        if (!hasWork)
        {
            _perFilePreviewEdits.Remove(key);
            return;
        }

        _perFilePreviewEdits[key] = new PerFilePreviewEditState
        {
            CleanOps = _watermarkCleanOps.ToList(),
            CloneOps = _textureCloneOps.ToList(),
            PasteOps = _selectionPasteOps.ToList(),
            ActiveCrop = _activeCropRect,
            PendingCrop = _pendingCropRect
        };
    }

    private void LoadPreviewEditsForFile(string? path)
    {
        _watermarkCleanOps.Clear();
        _textureCloneOps.Clear();
        _selectionPasteOps.Clear();
        _activeCropRect = null;
        _pendingCropRect = null;
        ClearSelectionPins();
        _cropUndoStack.Clear();
        CancelCropDrag();
        CancelFloatingPaste(clearCopy: false);
        _cloneSourceNorm = null;
        _clonePickSourceNext = false;
        _cloneHoverNorm = null;
        _clonePainting = false;
        _cloneLastStampNorm = null;
        _filigramBrushCenterCanvas = null;
        _filigramHoverNorm = null;
        if (FiligramBrushModeToggle is not null)
            FiligramBrushModeToggle.IsChecked = false;
        _filigramBrushMode = false;

        if (!string.IsNullOrWhiteSpace(path)
            && _perFilePreviewEdits.TryGetValue(NormalizeEditFileKey(path), out var state))
        {
            _watermarkCleanOps.AddRange(state.CleanOps);
            _textureCloneOps.AddRange(state.CloneOps);
            _selectionPasteOps.AddRange(state.PasteOps);
            _activeCropRect = state.ActiveCrop;
            _pendingCropRect = state.PendingCrop;
        }

        RefreshFiligramCleanButtonUi();
        RefreshCloneButtonsUi();
        RefreshCloneOverlay();
        SetCropOverlay(_pendingCropRect ?? _activeCropRect);
        UpdateCropUi();
    }

    private void ClearAllPerFilePreviewEdits()
    {
        _perFilePreviewEdits.Clear();
        _watermarkCleanOps.Clear();
        _textureCloneOps.Clear();
        _selectionPasteOps.Clear();
        _activeCropRect = null;
        _pendingCropRect = null;
        ClearSelectionPins();
        _cropUndoStack.Clear();
        CancelCropDrag();
        CancelFloatingPaste(clearCopy: true);
        ClearTextureCloneState();
        SetCropOverlay(null);
        UpdateCropUi();
        RefreshFiligramCleanButtonUi();
    }

    private void ResetCropStateForNewSource()
    {
        LoadPreviewEditsForFile(null);
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
        // Şablon değişince filigram/klon/kırp/logo sıfırlanmaz — yalnızca önizleme yenilenir
        PersistPreviewEditsForFile(_lastPreviewSourceForCropReset
                                   ?? TryGetPreviewSourceImageFile(GetActiveSourceFolder()));

        if (TemplateCombo.SelectedItem is TemplateListItem item)
        {
            TemplateDescription.Text = item.Template.IsPassthrough
                ? item.Description
                : $"{item.Description}\nBoyut: {item.SizeLabel}";
            TemplateFavoritesStore.TouchRecent(item.Template.Id);
            UpdateFavoriteButton();
            RefreshTemplateDependantUi(item);
            RefreshResponsiveFitUi();
            if (!HasActivePhotoOrLogoWork())
                SuggestColorPackForTemplate(item.Template.Id);
        }
        RefreshExportResolutionComboItems();
        ScheduleLivePreview();
    }

    private bool HasActivePhotoOrLogoWork()
    {
        if (_watermarkCleanOps.Count > 0 || _textureCloneOps.Count > 0)
            return true;
        if (_activeCropRect is not null || _pendingCropRect is not null)
            return true;
        if (_perFilePreviewEdits.Count > 0)
            return true;
        if (ShowBrandLogoCheck?.IsChecked == true)
            return true;
        return false;
    }

    /// <summary>
    /// Siyah şablonlarda koyu, beyaz şablonlarda açık palet öner.
    /// </summary>
    private void SuggestColorPackForTemplate(string templateId)
    {
        if (_loadingPreset || ColorPackCombo is null)
            return;

        bool wantsDark = templateId.Contains("black", StringComparison.OrdinalIgnoreCase)
                         || templateId.Contains("siyah", StringComparison.OrdinalIgnoreCase)
                         || templateId.Contains("dark", StringComparison.OrdinalIgnoreCase);
        bool wantsLight = templateId.Contains("white", StringComparison.OrdinalIgnoreCase)
                          || templateId.Contains("beyaz", StringComparison.OrdinalIgnoreCase);

        if (!wantsDark && !wantsLight)
            return;

        if (ColorPackCombo.SelectedItem is ColorPackListItem cur && cur.Theme.IsCustom)
            return;

        bool bgIsLight = true;
        if (ColorPackCombo.SelectedItem is ColorPackListItem pack && !pack.Theme.IsCustom)
        {
            var hex = (pack.Theme.BackgroundHex ?? "").Trim().TrimStart('#');
            bgIsLight = hex.Length >= 6 && IsHexLight(hex);
        }

        if (wantsDark && !bgIsLight)
            return;
        if (wantsLight && bgIsLight)
            return;

        string[] prefer = wantsDark
            ? ["gece", "gece-altin", "antrasit"]
            : ["beyaz", "klasik", "monokrom"];

        foreach (ColorPackListItem item in ColorPackCombo.Items)
        {
            if (prefer.Contains(item.Theme.Id, StringComparer.OrdinalIgnoreCase))
            {
                ColorPackCombo.SelectedItem = item;
                return;
            }
        }
    }

    private static bool IsHexLight(string hex6)
    {
        try
        {
            int r = Convert.ToInt32(hex6.Substring(0, 2), 16);
            int g = Convert.ToInt32(hex6.Substring(2, 2), 16);
            int b = Convert.ToInt32(hex6.Substring(4, 2), 16);
            double y = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
            return y > 0.55;
        }
        catch
        {
            return false;
        }
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
            SyncOutputFormatComboFromSaveAsPng(preset.SaveAsPng);
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
        var templateId = TemplateCombo.SelectedItem is TemplateListItem t ? t.Template.Id : "sablon-yok";
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
            SaveAsPng = OutputFormatCombo?.SelectedItem is OutputFormatListItem fmt ? fmt.SaveAsPng : SaveAsPngCheck.IsChecked == true,
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
        PersistPreviewEditsForFile(_lastPreviewSourceForCropReset
                                   ?? TryGetPreviewSourceImageFile(GetActiveSourceFolder()));

        var template = TemplateCombo.SelectedItem is TemplateListItem t ? t.Template : null;
        bool resizeOnly = ResizeOnlyCheck.IsChecked == true;

        var selectedFiles = SourceFileList.SelectedItems
            .Cast<string>()
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();
        bool cropOnlySelected = _activeCropRect is not null && selectedFiles.Count > 0;

        var cleanByFile = new Dictionary<string, IReadOnlyList<WatermarkCleanOp>>(StringComparer.OrdinalIgnoreCase);
        var cloneByFile = new Dictionary<string, IReadOnlyList<TextureCloneOp>>(StringComparer.OrdinalIgnoreCase);
        var pasteByFile = new Dictionary<string, IReadOnlyList<SelectionPasteOp>>(StringComparer.OrdinalIgnoreCase);
        var cropByFile = new Dictionary<string, NormalizedCropRect>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, state) in _perFilePreviewEdits)
        {
            if (state.CleanOps.Count > 0)
                cleanByFile[key] = state.CleanOps.ToList();
            if (state.CloneOps.Count > 0)
                cloneByFile[key] = state.CloneOps.ToList();
            if (state.PasteOps.Count > 0)
                pasteByFile[key] = state.PasteOps.ToList();
            if (state.ActiveCrop is { } crop)
                cropByFile[key] = crop;
        }

        return new()
        {
            ResizeOnly = resizeOnly,
            StretchToExport = template?.StretchToExport == true && !resizeOnly,
            ResponsiveProductFit = ResponsiveProductFitCheck.IsChecked == true && !resizeOnly,
            ExtendTemplateEdges = ExtendTemplateEdgesCheck?.IsChecked != false && !resizeOnly,
            EdgePadSampleRect = _edgePadSampleRect,
            JpegQuality = (int)JpegQualitySlider.Value,
            SaveAsPng = OutputFormatCombo?.SelectedItem is OutputFormatListItem fmt ? fmt.SaveAsPng : SaveAsPngCheck.IsChecked == true,
            FileNamePattern = string.IsNullOrWhiteSpace(OutputFileNameBox.Text)
                ? OutputFileNamer.DefaultPattern
                : OutputFileNameBox.Text.Trim(),
            TextOverlay = BuildTextOverlaySettings(),
            SamplePreviewCount = ParseSampleCount(),
            ProcessOnlySelectedFiles = ProcessSelectedOnlyCheck.IsChecked == true,
            CropRect = _activeCropRect,
            CropOnlySelectedFiles = cropOnlySelected && cropByFile.Count == 0,
            CropSelectedFilePaths = cropOnlySelected && cropByFile.Count == 0 ? selectedFiles : [],
            WatermarkCleanOps = _watermarkCleanOps.ToList(),
            TextureCloneOps = _textureCloneOps.ToList(),
            SelectionPasteOps = _selectionPasteOps.ToList(),
            WatermarkCleanOpsByFile = cleanByFile,
            TextureCloneOpsByFile = cloneByFile,
            SelectionPasteOpsByFile = pasteByFile,
            CropRectByFile = cropByFile
        };
    }

    private WatermarkCleanStyle GetSelectedFiligramCleanStyle()
    {
        if (FiligramCleanStyleCombo?.SelectedItem is FiligramStyleItem item)
            return item.Style;
        if (FiligramCleanStyleCombo?.SelectedValue is WatermarkCleanStyle s)
            return s;
        return WatermarkCleanStyle.Cloud;
    }

    private void InitializeFiligramCleanStyleCombo()
    {
        if (FiligramCleanStyleCombo is null)
            return;
        FiligramCleanStyleCombo.Items.Clear();
        FiligramCleanStyleCombo.DisplayMemberPath = "Name";
        FiligramCleanStyleCombo.SelectedValuePath = "Style";
        FiligramCleanStyleCombo.Items.Add(new FiligramStyleItem("Bulut / duman", WatermarkCleanStyle.Cloud));
        FiligramCleanStyleCombo.Items.Add(new FiligramStyleItem("Profesyonel yumuşak", WatermarkCleanStyle.SoftHeal));
        FiligramCleanStyleCombo.Items.Add(new FiligramStyleItem("Kusursuz geçiş", WatermarkCleanStyle.Seamless));
        FiligramCleanStyleCombo.Items.Add(new FiligramStyleItem("Doku eşle", WatermarkCleanStyle.TextureFill));
        FiligramCleanStyleCombo.Items.Add(new FiligramStyleItem("Derin bulanık", WatermarkCleanStyle.DeepBlur));
        FiligramCleanStyleCombo.Items.Add(new FiligramStyleItem("Blok", WatermarkCleanStyle.Block));
        FiligramCleanStyleCombo.Items.Add(new FiligramStyleItem("Keskin kenar", WatermarkCleanStyle.SharpEdge));
        FiligramCleanStyleCombo.SelectedIndex = 0;
    }

    private void InitializeCloneBrushShapeCombo()
    {
        // Görsel şekil çubuğu (Normal varsayılan) — ComboBox yok
        if (ShapeNormalRadio is not null)
            ShapeNormalRadio.IsChecked = true;
        _selectedBrushShape = TextureCloneBrushShape.Normal;
    }

    private TextureCloneBrushShape _selectedBrushShape = TextureCloneBrushShape.Normal;

    private void BrushShapeRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string tag })
            return;

        _selectedBrushShape = tag switch
        {
            "Circle" => TextureCloneBrushShape.Circle,
            "Square" => TextureCloneBrushShape.Square,
            "SoftSquare" => TextureCloneBrushShape.SoftSquare,
            "Ellipse" => TextureCloneBrushShape.Ellipse,
            _ => TextureCloneBrushShape.Normal
        };
        if (IsPinSelectMode && _selectionPins.Count > 0)
            ApplyPinsToPendingSelection();
        else
        {
            RefreshCloneOverlay();
            RefreshFiligramBrushOverlay();
        }
        SyncFiligramBrushPendingFromTools();
        if (CloneStatusHint is not null && IsFiligramBrushMode)
        {
            CloneStatusHint.Text = _selectedBrushShape == TextureCloneBrushShape.Normal
                ? "→ Normal seçim: tıkla / sürükle çerçeve"
                : "→ Şekil/Boyut ayarla, tıkla: filigram alanı";
        }
    }

    private TextureCloneBrushShape GetSelectedCloneBrushShape() => _selectedBrushShape;

    /// <summary>Klon damga için: Normal → daire.</summary>
    private TextureCloneBrushShape GetEffectiveCloneStampShape() =>
        _selectedBrushShape == TextureCloneBrushShape.Normal
            ? TextureCloneBrushShape.Circle
            : _selectedBrushShape;

    /// <summary>Filigram/önizleme için: Normal → kare çerçeve.</summary>
    private TextureCloneBrushShape GetEffectiveFiligramBrushShape() =>
        _selectedBrushShape == TextureCloneBrushShape.Normal
            ? TextureCloneBrushShape.Square
            : _selectedBrushShape;

    private void CloneBrushSizeMinus_Click(object sender, RoutedEventArgs e) =>
        AdjustCloneBrushSize(-1);

    private void CloneBrushSizePlus_Click(object sender, RoutedEventArgs e) =>
        AdjustCloneBrushSize(+1);

    private void AdjustCloneBrushSize(int direction)
    {
        if (CloneBrushSizeSlider is null)
            return;

        double v = CloneBrushSizeSlider.Value;
        double next;
        if (direction < 0)
        {
            if (v > 1.0 + 1e-6)
                next = Math.Round(v) - 1.0;
            else if (v > 0.95) // ≈1 → 0.9
                next = 0.9;
            else
                next = Math.Round(v - 0.1, 1);
        }
        else
        {
            if (v < 1.0 - 1e-6)
            {
                next = Math.Round(v + 0.1, 1);
                if (next > 1.0)
                    next = 1.0;
            }
            else
                next = Math.Max(1.0, Math.Round(v)) + 1.0;
        }

        CloneBrushSizeSlider.Value = Math.Clamp(next, CloneBrushSizeSlider.Minimum, CloneBrushSizeSlider.Maximum);
    }

    private void RefreshFiligramCleanButtonUi()
    {
        if (FiligramCleanUndoButton is not null)
            FiligramCleanUndoButton.IsEnabled = _watermarkCleanOps.Count > 0;
        if (CleanCornerWatermarkButton is not null)
        {
            CleanCornerWatermarkButton.Content = _watermarkCleanOps.Count > 0
                ? $"Filigram temizle ({_watermarkCleanOps.Count})"
                : "Filigram temizle";
        }
        if (FiligramBrushModeToggle is not null)
        {
            FiligramBrushModeToggle.Content = _filigramBrushCenterCanvas is not null
                ? "Filigram fırça ✓"
                : "Filigram fırça";
        }
        if (ResetAllPhotoEditsButton is not null)
            ResetAllPhotoEditsButton.IsEnabled = HasAnyPhotoEdits();
    }

    private bool IsFiligramBrushMode =>
        _filigramBrushMode || FiligramBrushModeToggle?.IsChecked == true;

    private void FiligramBrushModeToggle_Changed(object sender, RoutedEventArgs e)
    {
        _filigramBrushMode = FiligramBrushModeToggle?.IsChecked == true;
        if (_filigramBrushMode)
        {
            if (CloneStampModeToggle?.IsChecked == true)
                CloneStampModeToggle.IsChecked = false;
            if (PinSelectModeToggle?.IsChecked == true)
                PinSelectModeToggle.IsChecked = false;
            CancelCropDrag();
            // Beyaz seçim varsa koru — filigram temizle / şekil bu alanı kullanır
            if (LivePreviewImage.Visibility == Visibility.Visible)
                LivePreviewImage.Cursor = Cursors.Cross;
            if (CloneStatusHint is not null)
                CloneStatusHint.Text = _pendingCropRect is not null
                    ? "→ Mevcut seçim kullanılacak (Filigram temizle)"
                    : GetSelectedCloneBrushShape() == TextureCloneBrushShape.Normal
                        ? "→ Normal seçim: boyut ayarla, tıkla"
                        : "→ Şekil/Boyut ayarla, tıkla: filigram alanı";
        }
        else
        {
            _filigramHoverNorm = null;
            _filigramBrushCenterCanvas = null;
            if (_eyedropperColorField is null && !IsCloneStampMode && !IsPinSelectMode)
                LivePreviewImage.Cursor = Cursors.Cross;
            if (CloneStatusHint is not null && !IsCloneStampMode)
                CloneStatusHint.Text = "";
        }
        RefreshFiligramCleanButtonUi();
        RefreshSelectionOverlaysAfterModeChange();
    }

    private void SyncFiligramBrushPendingFromTools()
    {
        if (_filigramBrushCenterCanvas is null)
            return;
        // Boyut/şekil değişince görsel güncellenir; merkez sabit
        RefreshFiligramBrushOverlay();
    }

    private void PlaceFiligramBrushAt(Point canvasNorm, bool applyNow)
    {
        _filigramBrushCenterCanvas = canvasNorm;
        ClearSelectionPins();
        _pendingCropRect = null;
        SetCropOverlay(null);
        RefreshFiligramCleanButtonUi();
        RefreshFiligramBrushOverlay();
        UpdateCropUi();

        if (applyNow)
            ApplyFiligramBrushClean();
    }

    private void ApplyFiligramBrushClean()
    {
        if (_filigramBrushCenterCanvas is not { } center)
            return;

        var style = GetSelectedFiligramCleanStyle();
        var src = CanvasNormToSourcePoint(center.X, center.Y);
        _watermarkCleanOps.Add(new WatermarkCleanOp(
            style,
            [],
            src,
            GetCloneRadiusNorm(),
            GetEffectiveFiligramBrushShape()));
        // Temizlik sonrası seçim çerçevesi takılı kalmasın — sadece hover kalsın
        _filigramBrushCenterCanvas = null;
        RefreshFiligramCleanButtonUi();
        RefreshFiligramBrushOverlay();
        ScheduleLivePreview();
    }

    private void RefreshFiligramBrushOverlay()
    {
        // Clone canvas üzerinde filigram fırçasını da çiz (klon yokken)
        if (IsCloneStampMode)
            return;
        RefreshCloneOverlay(); // ortak çizim: filigram fırça halkası
    }

    private void CleanCornerWatermarkButton_Click(object sender, RoutedEventArgs e)
    {
        var style = GetSelectedFiligramCleanStyle();
        WatermarkCleanOp? op = null;
        var brushShape = GetEffectiveFiligramBrushShape();

        // 1) Şekilli tek pin / filigram fırça merkezi
        if (_filigramBrushCenterCanvas is { } brushCenter
            && GetSelectedCloneBrushShape() != TextureCloneBrushShape.Normal)
        {
            op = new WatermarkCleanOp(
                style,
                [],
                CanvasNormToSourcePoint(brushCenter.X, brushCenter.Y),
                GetCloneRadiusNorm(),
                brushShape);
        }
        else if (_selectionPins.Count >= 2)
        {
            var poly = _selectionPins
                .Select(p => CanvasNormToSourcePoint(p.X, p.Y))
                .ToList();
            op = new WatermarkCleanOp(style, poly);
        }
        else if (_selectionPins.Count == 1
                 && GetSelectedCloneBrushShape() != TextureCloneBrushShape.Normal)
        {
            var pin = _selectionPins[0];
            op = new WatermarkCleanOp(
                style,
                [],
                CanvasNormToSourcePoint(pin.X, pin.Y),
                GetCloneRadiusNorm(),
                brushShape);
        }
        else if (_pendingCropRect is { } rect)
        {
            var poly = new[]
            {
                CanvasNormToSourcePoint(rect.Left, rect.Top),
                CanvasNormToSourcePoint(rect.Left + rect.Width, rect.Top),
                CanvasNormToSourcePoint(rect.Left + rect.Width, rect.Top + rect.Height),
                CanvasNormToSourcePoint(rect.Left, rect.Top + rect.Height)
            };
            op = new WatermarkCleanOp(style, poly);
        }

        if (op is null)
        {
            if (FiligramBrushModeToggle is not null)
                FiligramBrushModeToggle.IsChecked = true;
            MessageBox.Show(
                "Filigram alanını şekil ile seçin:" + Environment.NewLine
                + "1) Şekil + Boyut seçin" + Environment.NewLine
                + "2) «Filigram fırça» açın" + Environment.NewLine
                + "3) Önizlemede tıklayın (veya sürükleyin)" + Environment.NewLine
                + "4) «Filigram temizle»ye basın — fırça modunda tık doğrudan da temizler" + Environment.NewLine + Environment.NewLine
                + "Alternatif: pin çokgeni veya dikdörtgen sürükleme.",
                "Filigram alanı gerekli",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        _watermarkCleanOps.Add(op);
        _filigramBrushCenterCanvas = null;
        _filigramHoverNorm = null;
        RefreshFiligramCleanButtonUi();
        RefreshFiligramBrushOverlay();
        ScheduleLivePreview();
    }

    private NormalizedPoint CanvasNormToSourcePoint(double cx, double cy)
    {
        ProductPlacementContext.CanvasNormToSourceNorm(cx, cy, out double sx, out double sy);
        return new NormalizedPoint(sx, sy);
    }

    private Point SourceNormToCanvasPoint(Point sourceNorm)
    {
        ProductPlacementContext.SourceNormToCanvasNorm(sourceNorm.X, sourceNorm.Y, out double cx, out double cy);
        return new Point(cx, cy);
    }

    private void FiligramCleanUndoButton_Click(object sender, RoutedEventArgs e)
    {
        if (_watermarkCleanOps.Count == 0)
            return;
        _watermarkCleanOps.RemoveAt(_watermarkCleanOps.Count - 1);
        RefreshFiligramCleanButtonUi();
        ScheduleLivePreview();
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


    private void InitializeOutputFormatCombo()
    {
        if (OutputFormatCombo is null)
            return;

        OutputFormatCombo.ItemsSource = new[]
        {
            new OutputFormatListItem("JPEG (.jpg) — varsayılan", false),
            new OutputFormatListItem("PNG (.png)", true)
        };
        OutputFormatCombo.DisplayMemberPath = "Label";
        OutputFormatCombo.SelectedIndex = 0;
        if (SaveAsPngCheck is not null)
            SaveAsPngCheck.IsChecked = false;
    }

    private void OutputFormatCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingPreset || OutputFormatCombo?.SelectedItem is not OutputFormatListItem item)
            return;
        if (SaveAsPngCheck is not null)
            SaveAsPngCheck.IsChecked = item.SaveAsPng;
        ScheduleLivePreview();
    }

    private void SyncOutputFormatComboFromSaveAsPng(bool saveAsPng)
    {
        if (OutputFormatCombo?.ItemsSource is null)
            return;
        foreach (OutputFormatListItem item in OutputFormatCombo.Items)
        {
            if (item.SaveAsPng == saveAsPng)
            {
                OutputFormatCombo.SelectedItem = item;
                return;
            }
        }
    }

    private void JpegQualitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (JpegQualityLabel is not null)
            JpegQualityLabel.Text = $"{(int)JpegQualitySlider.Value}";
        // JPEG kalitesi yalnızca kayıtta; önizlemeyi yenileme
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
        if (!_previewReady || _suppressSourceSelectionHandler)
            return;

        UpdateLivePreviewFocusFromSelection(e);
        OnSourceFileSelectionChanged();
    }

    private void SourceFileList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_previewReady)
            return;
        if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != 0)
            return;

        var item = FindSourceListBoxItem(e.OriginalSource as DependencyObject);
        if (item?.Content is not string path || !File.Exists(path))
            return;

        bool changed = !string.Equals(_livePreviewSourceFile, path, StringComparison.OrdinalIgnoreCase);
        _livePreviewSourceFile = path;
        if (changed && item.IsSelected)
            OnSourceFileSelectionChanged();
    }

    private static ListBoxItem? FindSourceListBoxItem(DependencyObject? origin)
    {
        while (origin is not null)
        {
            if (origin is ListBoxItem lbi)
                return lbi;
            origin = VisualTreeHelper.GetParent(origin);
        }
        return null;
    }

    private void UpdateLivePreviewFocusFromSelection(SelectionChangedEventArgs e)
    {
        if (SourceFileList.SelectedItems.Count == 0)
        {
            _livePreviewSourceFile = null;
            return;
        }

        // Tek dosya eklendi (normal tık / Ctrl ile ekleme) → o dosya önizleme odağı
        if (e.AddedItems.Count == 1 && e.AddedItems[0] is string added && File.Exists(added))
        {
            _livePreviewSourceFile = added;
            return;
        }

        // Tek seçim kaldı
        if (SourceFileList.SelectedItems.Count == 1
            && SourceFileList.SelectedItems[0] is string only
            && File.Exists(only))
        {
            _livePreviewSourceFile = only;
            return;
        }

        // Select All / Shift aralığı: mevcut odak hâlâ seçiliyse koru
        if (!string.IsNullOrEmpty(_livePreviewSourceFile)
            && IsSourcePathSelected(_livePreviewSourceFile)
            && File.Exists(_livePreviewSourceFile))
        {
            return;
        }

        _livePreviewSourceFile = FirstSelectedSourcePath();
    }

    private bool IsSourcePathSelected(string path)
    {
        var key = BrandLogoResolver.NormalizePath(path);
        foreach (var item in SourceFileList.SelectedItems)
        {
            if (item is string p && BrandLogoResolver.NormalizePath(p) == key)
                return true;
        }
        return false;
    }

    private string? FirstSelectedSourcePath()
    {
        foreach (var item in SourceFileList.SelectedItems)
        {
            if (item is string path && File.Exists(path))
                return path;
        }
        return null;
    }

    private void SelectAllFiles_Click(object sender, RoutedEventArgs e)
    {
        if (SourceFileList.Items.Count > 0)
            SourceFileList.SelectAll();
    }

    private void RefreshSourceFileList()
    {
        var previousSelected = SourceFileList.SelectedItems.Cast<object>()
            .OfType<string>()
            .Where(File.Exists)
            .ToList();
        var focus = _livePreviewSourceFile;

        _suppressSourceSelectionHandler = true;
        try
        {
            SourceFileList.Items.Clear();
            var path = GetActiveSourceFolder();
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                _livePreviewSourceFile = null;
                _lastPreviewSourceForCropReset = null;
                return;
            }

            var files = BatchProcessor.FindImages(path).ToList();
            foreach (var file in files)
                SourceFileList.Items.Add(file);

            var fileSet = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);
            foreach (var sel in previousSelected)
            {
                if (!fileSet.Contains(sel))
                    continue;
                foreach (var item in SourceFileList.Items)
                {
                    if (item is string p && string.Equals(p, sel, StringComparison.OrdinalIgnoreCase))
                    {
                        SourceFileList.SelectedItems.Add(item);
                        break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(focus) && fileSet.Contains(focus))
                _livePreviewSourceFile = focus;
            else if (SourceFileList.SelectedItems.Count > 0)
                _livePreviewSourceFile = FirstSelectedSourcePath();
            else
                _livePreviewSourceFile = files.FirstOrDefault();
        }
        finally
        {
            _suppressSourceSelectionHandler = false;
        }
    }

    private void SourceDropZone_DragOver(object sender, DragEventArgs e) =>
        SourcePathsDragOver(e);

    private void SourceDropZone_Drop(object sender, DragEventArgs e) =>
        ApplyDroppedSourcePaths(e);

    private void LivePreviewDropZone_DragOver(object sender, DragEventArgs e) =>
        SourcePathsDragOver(e);

    private void LivePreviewDropZone_Drop(object sender, DragEventArgs e) =>
        ApplyDroppedSourcePaths(e);

    /// <summary>
    /// DragOver sırasında FileDrop verisine dokunma — Explorer OLE sürüklemesinde
    /// GetData çağrısı bırakmayı (Copy imlecini) bozar. Sadece GetDataPresent yeter.
    /// </summary>
    private static void SourcePathsDragOver(DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private static bool TryGetDroppedSourcePaths(
        DragEventArgs e,
        out string? folder,
        out List<string> files)
    {
        folder = null;
        files = [];
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return false;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
            return false;

        folder = paths.FirstOrDefault(Directory.Exists);
        files = paths.Where(File.Exists)
            .Where(p => ImageInputCatalog.IsSupportedExtension(Path.GetExtension(p)))
            .ToList();
        return folder is not null || files.Count > 0;
    }

    private void ApplyDroppedSourcePaths(DragEventArgs e)
    {
        if (!TryGetDroppedSourcePaths(e, out var folder, out var files))
            return;

        e.Handled = true;

        // Klasör bırakıldı → kaynak klasörü + panel listesi
        if (folder is not null && files.Count == 0)
        {
            SourceFolderBox.Text = folder;
            return;
        }

        // Görsel(ler) bırakıldı → üst klasör yolu otomatik, panelde seç, önizle
        if (files.Count == 0)
            return;

        var parent = Path.GetDirectoryName(files[0]);
        if (!string.IsNullOrEmpty(parent))
            SourceFolderBox.Text = parent;

        RefreshSourceFileList();
        _suppressSourceSelectionHandler = true;
        try
        {
            SourceFileList.SelectedItems.Clear();
            foreach (var file in files)
            {
                for (int i = 0; i < SourceFileList.Items.Count; i++)
                {
                    if (string.Equals(SourceFileList.Items[i]?.ToString(), file, StringComparison.OrdinalIgnoreCase))
                        SourceFileList.SelectedItems.Add(SourceFileList.Items[i]);
                }
            }
        }
        finally
        {
            _suppressSourceSelectionHandler = false;
        }

        _livePreviewSourceFile = files[0];
        ProcessSelectedOnlyCheck.IsChecked = true;
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
            int sw = 0, sh = 0;
            if (!string.IsNullOrWhiteSpace(_livePreviewSourceFile) && File.Exists(_livePreviewSourceFile))
            {
                try
                {
                    using var img = PreviewSourceCache.GetClone(_livePreviewSourceFile);
                    sw = img.Width;
                    sh = img.Height;
                }
                catch
                {
                    // ignore — min boyut kullan
                }
            }

            var size = sw > 0 && sh > 0
                ? templateItem.Template.ResolveOutputSize(sw, sh)
                : templateItem.Template.OutputSize;
            tw = size.Width;
            th = size.Height;
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
        LivePreviewImage.Cursor = CropModeCheck?.IsChecked == true ? Cursors.Cross : Cursors.Arrow;
        Mouse.OverrideCursor = null;
    }

    private bool IsCropModeEnabled => CropModeCheck?.IsChecked == true;

    private void UpdateCropUi()
    {
        if (CropUndoButton is not null)
            CropUndoButton.IsEnabled = _cropUndoStack.Count > 0;
        if (CropClearButton is not null)
            CropClearButton.IsEnabled = _activeCropRect is not null
                                        || _pendingCropRect is not null
                                        || _filigramBrushCenterCanvas is not null
                                        || _selectionPins.Count > 0;
        if (RestoreLastSelectionButton is not null)
            RestoreLastSelectionButton.IsEnabled = _lastClearedSelection is not null;
        if (ResetAllPhotoEditsButton is not null)
            ResetAllPhotoEditsButton.IsEnabled = HasAnyPhotoEdits();
        if (CropApplyButton is not null)
            CropApplyButton.IsEnabled = _pendingCropRect is not null
                                        || (_selectionPins.Count >= 2)
                                        || (_selectionPins.Count == 1 && GetSelectedCloneBrushShape() != TextureCloneBrushShape.Normal);
        if (CropInteriorButton is not null)
            CropInteriorButton.IsEnabled = CropApplyButton?.IsEnabled == true;
        if (TextureFillSelectionButton is not null)
            TextureFillSelectionButton.IsEnabled =
                (_cloneSourceNorm is not null)
                && (CropApplyButton?.IsEnabled == true);
        if (EdgePadFromSelectionButton is not null)
            EdgePadFromSelectionButton.IsEnabled =
                CropApplyButton?.IsEnabled == true
                || _filigramBrushCenterCanvas is not null
                || _pendingCropRect is not null
                || _selectionPins.Count >= 2;
        bool hasSel = CropApplyButton?.IsEnabled == true
                      || _filigramBrushCenterCanvas is not null
                      || (_pendingCropRect is not null);
        if (SelectionCopyButton is not null)
            SelectionCopyButton.IsEnabled = hasSel;
        if (SelectionPasteButton is not null)
        {
            SelectionPasteButton.IsEnabled = hasSel || _copiedSelectionPng is { Length: > 0 } || _floatingPasteActive;
            SelectionPasteButton.Content = _floatingPasteActive ? "Bırak ✓" : "Bırak / Döndür";
        }
        if (SelectionRotateLeftButton is not null)
            SelectionRotateLeftButton.IsEnabled = _floatingPasteActive;
        if (SelectionRotateRightButton is not null)
            SelectionRotateRightButton.IsEnabled = _floatingPasteActive;
        SyncCropPxBoxesFromPending();
    }

    private bool TryCaptureSelectionSnapshot(out SelectionSnapshot snapshot)
    {
        bool has = _pendingCropRect is not null
                   || _selectionPins.Count > 0
                   || _filigramBrushCenterCanvas is not null;
        if (!has)
        {
            snapshot = null!;
            return false;
        }

        snapshot = new SelectionSnapshot
        {
            Pending = _pendingCropRect,
            Pins = _selectionPins.Select(p => new Point(p.X, p.Y)).ToList(),
            FiligramCenter = _filigramBrushCenterCanvas,
            BrushSizePct = CloneBrushSizeSlider?.Value
        };
        return true;
    }

    private void RememberClearedSelection()
    {
        if (TryCaptureSelectionSnapshot(out var snap))
            _lastClearedSelection = snap;
    }

    private void RestoreLastSelectionButton_Click(object sender, RoutedEventArgs e) =>
        RestoreLastClearedSelection();

    private void RestoreLastClearedSelection()
    {
        if (_lastClearedSelection is null)
            return;

        var snap = _lastClearedSelection;
        _selectionPins.Clear();
        foreach (var p in snap.Pins)
            _selectionPins.Add(p);

        _pendingCropRect = snap.Pending;
        _filigramBrushCenterCanvas = snap.FiligramCenter;
        if (snap.BrushSizePct is double pct && CloneBrushSizeSlider is not null)
        {
            _updatingBrushFromPin = true;
            CloneBrushSizeSlider.Value = Math.Clamp(pct, CloneBrushSizeSlider.Minimum, CloneBrushSizeSlider.Maximum);
            _updatingBrushFromPin = false;
        }

        if (_selectionPins.Count > 0)
            ApplyPinsToPendingSelection();
        else
        {
            SetCropOverlay(ShouldShowPendingCropOverlayVisual ? _pendingCropRect : null);
            RefreshCloneOverlay();
        }

        RefreshPinOverlay();
        RefreshPinButtonsUi();
        RefreshFiligramBrushOverlay();
        UpdateCropUi();
    }

    private void CropModeCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsCropModeEnabled)
        {
            CancelCropDrag();
            if (_eyedropperColorField is null)
                LivePreviewImage.Cursor = Cursors.Arrow;
        }
        else if (_eyedropperColorField is null && LivePreviewImage.Visibility == Visibility.Visible)
        {
            LivePreviewImage.Cursor = Cursors.Cross;
        }

        UpdateCropUi();
    }

    private void CropApply_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingCropRect is null && _selectionPins.Count > 0)
            ApplyPinsToPendingSelection();
        if (_pendingCropRect is null)
            return;

        NormalizedCropRect newActive = _activeCropRect is { } oldCrop
            ? new NormalizedCropRect(
                oldCrop.Left + _pendingCropRect.Left * oldCrop.Width,
                oldCrop.Top + _pendingCropRect.Top * oldCrop.Height,
                _pendingCropRect.Width * oldCrop.Width,
                _pendingCropRect.Height * oldCrop.Height)
            : _pendingCropRect;

        _cropUndoStack.Push(_activeCropRect);
        _activeCropRect = newActive;
        RememberClearedSelection();
        _pendingCropRect = null;
        CancelCropDrag();
        UpdateCropUi();
        SetCropOverlay(null);
        ScheduleLivePreview();
    }

    /// <summary>Seçimin içini kaynak görselde temizler (dış çerçeve kalır).</summary>
    private void CropInterior_Click(object sender, RoutedEventArgs e)
    {
        var style = GetSelectedFiligramCleanStyle();
        // Bulut stili iç kesimde zayıf kalır — doku eşle varsayılanı
        if (style == WatermarkCleanStyle.Cloud)
            style = WatermarkCleanStyle.TextureFill;

        if (!TryBuildSelectionCleanOp(style, out var op) || op is null)
        {
            MessageBox.Show(
                "İçini kırmak için önce bir seçim yapın (dikdörtgen, pin veya şekil).",
                "İçini kırp",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        _watermarkCleanOps.Add(op);
        RefreshFiligramCleanButtonUi();
        UpdateCropUi();
        ScheduleLivePreview();
    }

    /// <summary>Klon kaynağından seçim alanına doku nakli (şablon tuvali uzayı).</summary>
    private void TextureFillSelection_Click(object sender, RoutedEventArgs e)
    {
        if (_cloneSourceNorm is null)
        {
            MessageBox.Show(
                "Önce «Kaynak al» ile doku kaynağını seçin, sonra seçime nakledin.",
                "Doku nakli",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (!TryGetSelectionCanvasRect(out var canvasRect, out var destCenter))
        {
            MessageBox.Show(
                "Doku nakli için önce bir seçim alanı oluşturun.",
                "Doku nakli",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var src = new NormalizedPoint(_cloneSourceNorm.Value.X, _cloneSourceNorm.Value.Y);
        _textureCloneOps.Add(new TextureCloneOp(
            src,
            new NormalizedPoint(destCenter.X, destCenter.Y),
            RadiusNorm: 0.05,
            GetEffectiveCloneStampShape(),
            FillRect: canvasRect));
        RefreshCloneButtonsUi();
        UpdateCropUi();
        ScheduleLivePreview();
    }

    /// <summary>
    /// Seçilen ince şeridi sol/sağ veya üst/alt letterbox’a uzatır.
    /// Dikey seçim → sol+sağ; yatay seçim → üst+alt.
    /// </summary>
    private void EdgePadFromSelection_Click(object sender, RoutedEventArgs e)
    {
        if (ExtendTemplateEdgesCheck is not null)
            ExtendTemplateEdgesCheck.IsChecked = true;

        if (TryGetSelectionCanvasRect(out var canvasRect, out _))
        {
            _edgePadSampleRect = canvasRect;
            if (CloneStatusHint is not null)
            {
                bool vertical = canvasRect.Height >= canvasRect.Width * 1.25;
                CloneStatusHint.Text = vertical
                    ? "→ Dikey şerit sol/sağ boşluğa uzatılacak"
                    : "→ Yatay şerit üst/alt boşluğa uzatılacak";
            }
        }
        else
        {
            MessageBox.Show(
                "Örnek şerit için iki nokta veya ince bir dikdörtgen seçin." + Environment.NewLine
                + "• Dikey şerit → sol ve sağ boşluk" + Environment.NewLine
                + "• Yatay şerit → üst ve alt boşluk" + Environment.NewLine + Environment.NewLine
                + "Seçim yapmadan «Kenarları uzat» kutusunu açarsanız fotoğraf kenarı otomatik kullanılır.",
                "Şeridi kenara uzat",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        ScheduleLivePreview();
    }

    /// <summary>Seçimi önizleme/şablon tuvali normalize dikdörtgeni olarak alır (klon için).</summary>
    private bool TryGetSelectionCanvasRect(out NormalizedCropRect canvasRect, out Point destCenter)
    {
        canvasRect = null!;
        destCenter = default;

        if (_pendingCropRect is null && _selectionPins.Count > 0)
            ApplyPinsToPendingSelection();

        if (_filigramBrushCenterCanvas is { } brush
            && GetSelectedCloneBrushShape() != TextureCloneBrushShape.Normal)
        {
            double rad = GetCloneRadiusNorm();
            double halfX = Math.Clamp(rad, 0.002, 0.35);
            double halfY = GetEffectiveFiligramBrushShape() == TextureCloneBrushShape.Ellipse
                ? halfX * 0.62
                : halfX;
            double left = Math.Clamp(brush.X - halfX, 0, 1);
            double top = Math.Clamp(brush.Y - halfY, 0, 1);
            canvasRect = new NormalizedCropRect(
                left, top,
                Math.Min(1 - left, halfX * 2),
                Math.Min(1 - top, halfY * 2));
            destCenter = new Point(brush.X, brush.Y);
            return true;
        }

        if (_selectionPins.Count >= 2)
        {
            double minX = _selectionPins.Min(p => p.X);
            double minY = _selectionPins.Min(p => p.Y);
            double maxX = _selectionPins.Max(p => p.X);
            double maxY = _selectionPins.Max(p => p.Y);
            canvasRect = new NormalizedCropRect(
                minX, minY,
                Math.Max(0.002, maxX - minX),
                Math.Max(0.002, maxY - minY));
            destCenter = new Point((minX + maxX) / 2, (minY + maxY) / 2);
            return true;
        }

        if (_selectionPins.Count == 1
            && GetSelectedCloneBrushShape() != TextureCloneBrushShape.Normal)
            ApplyPinsToPendingSelection();

        if (_pendingCropRect is { } r)
        {
            canvasRect = new NormalizedCropRect(
                Math.Clamp(r.Left, 0, 1),
                Math.Clamp(r.Top, 0, 1),
                Math.Max(0.002, Math.Min(1 - r.Left, r.Width)),
                Math.Max(0.002, Math.Min(1 - r.Top, r.Height)));
            destCenter = new Point(r.Left + r.Width / 2, r.Top + r.Height / 2);
            return true;
        }

        return false;
    }

    private bool TryGetSelectionSourceRect(out NormalizedCropRect sourceRect, out Point destCenter)
    {
        sourceRect = null!;
        destCenter = default;

        if (_pendingCropRect is null && _selectionPins.Count > 0)
            ApplyPinsToPendingSelection();

        // Şekil + merkez (pin yok)
        if (_filigramBrushCenterCanvas is { } brush
            && GetSelectedCloneBrushShape() != TextureCloneBrushShape.Normal)
        {
            var src = CanvasNormToSourcePoint(brush.X, brush.Y);
            double rad = GetCloneRadiusNorm();
            double halfX = Math.Clamp(rad, 0.002, 0.35);
            double halfY = GetEffectiveFiligramBrushShape() == TextureCloneBrushShape.Ellipse
                ? halfX * 0.62
                : halfX;
            double left = Math.Clamp(src.X - halfX, 0, 1);
            double top = Math.Clamp(src.Y - halfY, 0, 1);
            sourceRect = new NormalizedCropRect(
                left, top,
                Math.Min(1 - left, halfX * 2),
                Math.Min(1 - top, halfY * 2));
            destCenter = new Point(src.X, src.Y);
            return true;
        }

        if (_selectionPins.Count >= 2)
        {
            var pts = _selectionPins.Select(p => CanvasNormToSourcePoint(p.X, p.Y)).ToList();
            double minX = pts.Min(p => p.X);
            double minY = pts.Min(p => p.Y);
            double maxX = pts.Max(p => p.X);
            double maxY = pts.Max(p => p.Y);
            sourceRect = new NormalizedCropRect(
                minX, minY,
                Math.Max(0.002, maxX - minX),
                Math.Max(0.002, maxY - minY));
            destCenter = new Point((minX + maxX) / 2, (minY + maxY) / 2);
            return true;
        }

        if (_selectionPins.Count == 1
            && GetSelectedCloneBrushShape() != TextureCloneBrushShape.Normal)
            ApplyPinsToPendingSelection();

        if (_pendingCropRect is { } r)
        {
            var tl = CanvasNormToSourcePoint(r.Left, r.Top);
            var br = CanvasNormToSourcePoint(r.Left + r.Width, r.Top + r.Height);
            double minX = Math.Min(tl.X, br.X);
            double minY = Math.Min(tl.Y, br.Y);
            double maxX = Math.Max(tl.X, br.X);
            double maxY = Math.Max(tl.Y, br.Y);
            sourceRect = new NormalizedCropRect(
                minX, minY,
                Math.Max(0.002, maxX - minX),
                Math.Max(0.002, maxY - minY));
            destCenter = new Point((minX + maxX) / 2, (minY + maxY) / 2);
            return true;
        }

        return false;
    }

    private void PlaceShapedSelectionAt(Point canvasNorm)
    {
        ClearSelectionPins();
        _filigramBrushCenterCanvas = canvasNorm;
        double r = GetCloneRadiusNorm();
        double halfX = Math.Clamp(r, 0.002, 0.35);
        double halfY = GetEffectiveFiligramBrushShape() == TextureCloneBrushShape.Ellipse
            ? halfX * 0.62
            : halfX;
        _pendingCropRect = ClampNormRect(canvasNorm.X - halfX, canvasNorm.Y - halfY, halfX * 2, halfY * 2);
        SetCropOverlay(null); // araç/şekil modunda yeşil halka
        RefreshCloneOverlay();
        UpdateCropUi();
    }

    private void SelectionCopyButton_Click(object sender, RoutedEventArgs e) =>
        CopyCurrentSelectionToClipboard();

    private void SelectionPasteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_floatingPasteActive)
        {
            ConfirmFloatingPaste();
            return;
        }

        StartFloatingPaste(fromCurrentSelection: true);
    }

    private void SelectionRotateLeft_Click(object sender, RoutedEventArgs e) =>
        NudgeFloatingPasteRotation(-5);

    private void SelectionRotateRight_Click(object sender, RoutedEventArgs e) =>
        NudgeFloatingPasteRotation(+5);

    private void CopyCurrentSelectionToClipboard()
    {
        if (!TryGetSelectionSourceRect(out var sourceRect, out _))
        {
            MessageBox.Show(
                "Kopyalamak için önce şekil, dikdörtgen veya pin ile seçim yapın.",
                "Kopyala",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        string? file = _livePreviewSourceFile
                       ?? TryGetPreviewSourceImageFile(GetActiveSourceFolder());
        if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
        {
            MessageBox.Show("Kaynak görsel yok.", "Kopyala",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            using var src = PreviewSourceCache.GetClone(file);
            var shape = GetSelectedCloneBrushShape() == TextureCloneBrushShape.Normal
                ? TextureCloneBrushShape.Square
                : GetSelectedCloneBrushShape();
            if (!SelectionPasteService.TryExtractPatch(src, sourceRect, shape,
                    out var png, out int pw, out int ph))
            {
                MessageBox.Show("Seçim kopyalanamadı.", "Kopyala",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _copiedSelectionPng = png;
            _copiedSelectionShape = shape;
            _copiedPatchPixelW = pw;
            _copiedPatchPixelH = ph;
            UpdateCropUi();
            if (CloneStatusHint is not null)
                CloneStatusHint.Text = "→ Kopyalandı — Yapıştır · tekerlek döndür · tıkla bırak";
        }
        catch (Exception ex)
        {
            MessageBox.Show("Kopyalama hatası: " + ex.Message, "Kopyala",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void StartFloatingPaste(bool fromCurrentSelection = false)
    {
        Point? preferCenter = null;

        if (fromCurrentSelection || _copiedSelectionPng is null || _copiedSelectionPng.Length == 0)
        {
            if (TryGetSelectionSourceRect(out _, out var destCenterSrc))
            {
                ProductPlacementContext.SourceNormToCanvasNorm(
                    destCenterSrc.X, destCenterSrc.Y, out double cx, out double cy);
                preferCenter = new Point(cx, cy);
            }
            else if (TryGetSelectionCenterNorm(out var c0))
            {
                preferCenter = c0;
            }
            else
            {
                MessageBox.Show(
                    "Şekil seçip önizlemeye tıklayın (pin gerekmez), veya dikdörtgen çizin.",
                    "Bırak / Döndür",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            string? file = _livePreviewSourceFile
                           ?? TryGetPreviewSourceImageFile(GetActiveSourceFolder());
            if (string.IsNullOrWhiteSpace(file) || !File.Exists(file)
                || !TryGetSelectionSourceRect(out var sourceRect, out _))
            {
                MessageBox.Show("Seçim alınamadı.", "Bırak / Döndür",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                using var src = PreviewSourceCache.GetClone(file);
                var shape = GetSelectedCloneBrushShape() == TextureCloneBrushShape.Normal
                    ? TextureCloneBrushShape.Square
                    : GetSelectedCloneBrushShape();
                if (!SelectionPasteService.TryExtractPatch(src, sourceRect, shape,
                        out var png, out int pw, out int ph))
                {
                    MessageBox.Show("Seçim kopyalanamadı.", "Bırak / Döndür",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _copiedSelectionPng = png;
                _copiedSelectionShape = shape;
                _copiedPatchPixelW = pw;
                _copiedPatchPixelH = ph;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata: " + ex.Message, "Bırak / Döndür",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }

        if (_copiedSelectionPng is null || _copiedSelectionPng.Length == 0)
            return;

        _floatingPasteCenterCanvas = preferCenter
            ?? (TryGetSelectionCenterNorm(out var c) ? c : new Point(0.5, 0.5));
        _floatingPasteRotationDeg = 0;
        _floatingPasteActive = true;

        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource = new MemoryStream(_copiedSelectionPng);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            if (FloatingPasteImage is not null)
            {
                FloatingPasteImage.Source = bmp;
                FloatingPasteImage.Visibility = Visibility.Visible;
            }
        }
        catch
        {
            _floatingPasteActive = false;
            return;
        }

        if (CloneStampModeToggle?.IsChecked == true)
            CloneStampModeToggle.IsChecked = false;
        if (FiligramBrushModeToggle?.IsChecked == true)
            FiligramBrushModeToggle.IsChecked = false;
        if (PinSelectModeToggle?.IsChecked == true)
            PinSelectModeToggle.IsChecked = false;

        // Seçim çerçevesini bırak — yüzen parça taşınır/döndürülür
        RememberClearedSelection();
        _pendingCropRect = null;
        _filigramBrushCenterCanvas = null;
        ClearSelectionPins();
        SetCropOverlay(null);
        RefreshCloneOverlay();

        RefreshFloatingPasteOverlay();
        UpdateCropUi();
        if (CloneStatusHint is not null)
            CloneStatusHint.Text = "→ Sürükle · tekerlek döndür · çift tık / Enter bırak · Esc iptal";
    }

    private void ConfirmFloatingPaste()
    {
        if (!_floatingPasteActive || _copiedSelectionPng is null)
            return;

        ProductPlacementContext.CanvasNormToSourceNorm(
            _floatingPasteCenterCanvas.X, _floatingPasteCenterCanvas.Y,
            out double sx, out double sy);

        _selectionPasteOps.Add(new SelectionPasteOp(
            _copiedSelectionPng.ToArray(),
            new NormalizedPoint(sx, sy),
            _floatingPasteRotationDeg,
            _copiedSelectionShape));

        CancelFloatingPaste(clearCopy: false);
        UpdateCropUi();
        ScheduleLivePreview();
        if (CloneStatusHint is not null)
            CloneStatusHint.Text = "→ Bırakıldı — şekil seçip tekrar Bırak / Döndür";
    }

    private void CancelFloatingPaste(bool clearCopy)
    {
        _floatingPasteActive = false;
        _floatingPasteDragging = false;
        _floatingPasteRotationDeg = 0;
        if (FloatingPasteImage is not null)
        {
            FloatingPasteImage.Visibility = Visibility.Collapsed;
            FloatingPasteImage.Source = null;
        }
        if (clearCopy)
        {
            _copiedSelectionPng = null;
            _copiedPatchPixelW = 0;
            _copiedPatchPixelH = 0;
        }
        UpdateCropUi();
    }

    private void NudgeFloatingPasteRotation(double deltaDeg)
    {
        if (!_floatingPasteActive)
            return;
        _floatingPasteRotationDeg = (_floatingPasteRotationDeg + deltaDeg) % 360.0;
        if (FloatingPasteRotate is not null)
            FloatingPasteRotate.Angle = _floatingPasteRotationDeg;
        RefreshFloatingPasteOverlay();
    }

    private void NudgeFloatingPastePosition(int dxPx, int dyPx)
    {
        if (!_floatingPasteActive)
            return;
        int baseW = Math.Max(1, _previewPixelWidth);
        int baseH = Math.Max(1, _previewPixelHeight);
        double zoom = Math.Max(1.0, _previewZoom);
        _floatingPasteCenterCanvas = new Point(
            Math.Clamp(_floatingPasteCenterCanvas.X + dxPx / (baseW * zoom), 0, 1),
            Math.Clamp(_floatingPasteCenterCanvas.Y + dyPx / (baseH * zoom), 0, 1));
        RefreshFloatingPasteOverlay();
    }

    private void RefreshFloatingPasteOverlay()
    {
        if (!_floatingPasteActive || FloatingPasteImage is null || FloatingPasteImage.Source is null)
            return;
        if (!TryGetLetterboxMapping(out _, out var rw, out var rh, out var ox, out var oy))
            return;
        if (FloatingPasteImage.Parent is not UIElement parent || LivePreviewImage is null)
            return;

        var origin = LivePreviewImage.TranslatePoint(new Point(0, 0), parent);
        double cx = origin.X + ox + _floatingPasteCenterCanvas.X * rw;
        double cy = origin.Y + oy + _floatingPasteCenterCanvas.Y * rh;

        double scale = ProductPlacementContext.HasPlacement
            ? Math.Min(
                ProductPlacementContext.DestWidth / (double)Math.Max(1, ProductPlacementContext.SourceWidth),
                ProductPlacementContext.DestHeight / (double)Math.Max(1, ProductPlacementContext.SourceHeight))
              * (rw / Math.Max(1, ProductPlacementContext.CanvasWidth))
            : rw / Math.Max(1.0, _previewPixelWidth);

        double dispW = Math.Max(8, _copiedPatchPixelW * scale);
        double dispH = Math.Max(8, _copiedPatchPixelH * scale);
        FloatingPasteImage.Width = dispW;
        FloatingPasteImage.Height = dispH;
        FloatingPasteImage.Margin = new Thickness(cx - dispW / 2, cy - dispH / 2, 0, 0);
        if (FloatingPasteRotate is not null)
            FloatingPasteRotate.Angle = _floatingPasteRotationDeg;
        FloatingPasteImage.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
        FloatingPasteImage.VerticalAlignment = System.Windows.VerticalAlignment.Top;
    }

    private bool TryBuildSelectionCleanOp(WatermarkCleanStyle style, out WatermarkCleanOp? op)
    {
        op = null;
        var brushShape = GetEffectiveFiligramBrushShape();

        if (_selectionPins.Count >= 2)
        {
            var poly = _selectionPins
                .Select(p => CanvasNormToSourcePoint(p.X, p.Y))
                .ToList();
            op = new WatermarkCleanOp(style, poly);
            return true;
        }

        if (_selectionPins.Count == 1
            && GetSelectedCloneBrushShape() != TextureCloneBrushShape.Normal)
        {
            var pin = _selectionPins[0];
            op = new WatermarkCleanOp(
                style,
                [],
                CanvasNormToSourcePoint(pin.X, pin.Y),
                GetCloneRadiusNorm(),
                brushShape);
            return true;
        }

        if (_filigramBrushCenterCanvas is { } brushCenter
            && GetSelectedCloneBrushShape() != TextureCloneBrushShape.Normal)
        {
            op = new WatermarkCleanOp(
                style,
                [],
                CanvasNormToSourcePoint(brushCenter.X, brushCenter.Y),
                GetCloneRadiusNorm(),
                brushShape);
            return true;
        }

        if (_pendingCropRect is null && _selectionPins.Count > 0)
            ApplyPinsToPendingSelection();

        if (_pendingCropRect is { } rect)
        {
            var poly = new[]
            {
                CanvasNormToSourcePoint(rect.Left, rect.Top),
                CanvasNormToSourcePoint(rect.Left + rect.Width, rect.Top),
                CanvasNormToSourcePoint(rect.Left + rect.Width, rect.Top + rect.Height),
                CanvasNormToSourcePoint(rect.Left, rect.Top + rect.Height)
            };
            op = new WatermarkCleanOp(style, poly);
            return true;
        }

        return false;
    }

    private void CropUndo_Click(object sender, RoutedEventArgs e)
    {
        if (_cropUndoStack.Count == 0)
            return;

        _activeCropRect = _cropUndoStack.Pop();
        _pendingCropRect = null;
        CancelCropDrag();
        UpdateCropUi();
        SetCropOverlay(null);
        ScheduleLivePreview();
    }

    private void CropClear_Click(object sender, RoutedEventArgs e)
    {
        bool hadCrop = _activeCropRect is not null || _pendingCropRect is not null;
        bool hadBrush = _filigramBrushCenterCanvas is not null || _filigramHoverNorm is not null;
        bool hadPins = _selectionPins.Count > 0;
        if (!hadCrop && !hadBrush && !hadPins)
            return;

        RememberClearedSelection();

        if (_activeCropRect is not null)
            _cropUndoStack.Push(_activeCropRect);
        _activeCropRect = null;
        _pendingCropRect = null;
        _filigramBrushCenterCanvas = null;
        _filigramHoverNorm = null;
        ClearSelectionPins();
        CancelCropDrag();
        UpdateCropUi();
        SetCropOverlay(null);
        RefreshCloneOverlay();
        if (hadCrop)
            ScheduleLivePreview();
    }

    private bool HasAnyPhotoEdits() =>
        _watermarkCleanOps.Count > 0
        || _textureCloneOps.Count > 0
        || _selectionPasteOps.Count > 0
        || _edgePadSampleRect is not null
        || _activeCropRect is not null
        || _pendingCropRect is not null
        || _selectionPins.Count > 0
        || _filigramBrushCenterCanvas is not null
        || _cloneSourceNorm is not null
        || _copiedSelectionPng is not null
        || _floatingPasteActive
        || _cropUndoStack.Count > 0;

    private void ResetAllPhotoEditsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!HasAnyPhotoEdits())
            return;

        var result = MessageBox.Show(
            "Bu görsele uygulanan tüm işlemler silinecek:" + Environment.NewLine
            + "• Kırpma" + Environment.NewLine
            + "• Filigram temizleme" + Environment.NewLine
            + "• Klon damgaları" + Environment.NewLine
            + "• Pin / seçimler" + Environment.NewLine + Environment.NewLine
            + "Devam edilsin mi?",
            "Tümünü sıfırla",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
            return;

        ResetAllPhotoEdits(refreshPreview: true);
    }

    private void ResetAllPhotoEdits(bool refreshPreview)
    {
        _watermarkCleanOps.Clear();
        _textureCloneOps.Clear();
        _selectionPasteOps.Clear();
        _edgePadSampleRect = null;
        _activeCropRect = null;
        _pendingCropRect = null;
        _cropUndoStack.Clear();
        ClearSelectionPins();
        CancelCropDrag();
        ClearPendingWhiteSelection();
        CancelFloatingPaste(clearCopy: true);

        _filigramBrushCenterCanvas = null;
        _filigramHoverNorm = null;
        if (FiligramBrushModeToggle is not null)
            FiligramBrushModeToggle.IsChecked = false;
        _filigramBrushMode = false;

        _cloneSourceNorm = null;
        _clonePickSourceNext = false;
        _cloneHoverNorm = null;
        _clonePainting = false;
        _cloneLastStampNorm = null;
        if (CloneStampModeToggle is not null)
            CloneStampModeToggle.IsChecked = false;
        _cloneStampMode = false;

        if (PinSelectModeToggle is not null)
            PinSelectModeToggle.IsChecked = false;
        _pinSelectMode = false;
        _lastClearedSelection = null;

        if (!string.IsNullOrWhiteSpace(_livePreviewSourceFile))
            _perFilePreviewEdits.Remove(NormalizeEditFileKey(_livePreviewSourceFile));

        SetCropOverlay(null);
        RefreshFiligramCleanButtonUi();
        RefreshCloneButtonsUi();
        RefreshCloneOverlay();
        RefreshPinOverlay();
        RefreshPinButtonsUi();
        UpdateCropUi();

        if (refreshPreview)
            ScheduleLivePreview();
    }

    private void CancelCropDrag()
    {
        _isCropping = false;
        _pinDragging = false;
        _cropDragMode = "none";
        _cropResizeHandle = "";
        _cropRectAtDragStart = null;
        _pinsAtCropDragStart = null;
        if (LivePreviewImage.IsMouseCaptured)
            LivePreviewImage.ReleaseMouseCapture();
    }

    private bool IsCloneStampMode => _cloneStampMode || CloneStampModeToggle?.IsChecked == true;

    private double GetCloneRadiusNorm()
    {
        double pct = CloneBrushSizeSlider?.Value ?? 4;
        return Math.Clamp(pct / 100.0, 0.001, 0.20);
    }

    private void CloneStampModeToggle_Changed(object sender, RoutedEventArgs e)
    {
        _cloneStampMode = CloneStampModeToggle?.IsChecked == true;
        if (_cloneStampMode)
        {
            if (FiligramBrushModeToggle?.IsChecked == true)
                FiligramBrushModeToggle.IsChecked = false;
            if (PinSelectModeToggle?.IsChecked == true)
                PinSelectModeToggle.IsChecked = false;
            CancelCropDrag();
            // Kaynak yoksa sonraki tık kaynak seçsin (otomatik kilitleme yok)
            if (_cloneSourceNorm is null)
                _clonePickSourceNext = true;
            if (LivePreviewImage.Visibility == Visibility.Visible)
                LivePreviewImage.Cursor = Cursors.Cross;
        }
        else
        {
            _clonePickSourceNext = false;
            _cloneHoverNorm = null;
            _clonePainting = false;
            _cloneLastStampNorm = null;
            if (_eyedropperColorField is null && !IsPinSelectMode && !IsFiligramBrushMode)
                LivePreviewImage.Cursor = Cursors.Cross;
        }
        RefreshCloneButtonsUi();
        RefreshSelectionOverlaysAfterModeChange();
    }

    private void ClonePickSourceButton_Click(object sender, RoutedEventArgs e)
    {
        if (!IsCloneStampMode)
            CloneStampModeToggle.IsChecked = true;

        // Yeniden kaynak: her zaman sonraki sol tık (veya sağ tık) yeni kaynak olur.
        // Seçim merkezine otomatik bağlamayız — aksi halde hep ilk kaynak kalıyordu.
        _clonePickSourceNext = true;
        RefreshCloneButtonsUi();
        RefreshSelectionOverlaysAfterModeChange();
        if (CloneStatusHint is not null)
            CloneStatusHint.Text = "→ Yeni kaynak noktasına tıkla (sağ tık da olur)";
    }

    private void CloneBrushSizeSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (CloneBrushSizeLabel is not null)
        {
            double v = CloneBrushSizeSlider?.Value ?? 4;
            CloneBrushSizeLabel.Text = v < 1 ? $"{v:0.0}%" : $"{(int)Math.Round(v)}%";
        }
        if (!_updatingBrushFromPin && _selectionPins.Count == 1)
            ApplyPinsToPendingSelection();
        RefreshCloneOverlay();
        RefreshFiligramBrushOverlay();
    }

    private void CloneUndoButton_Click(object sender, RoutedEventArgs e)
    {
        if (_textureCloneOps.Count == 0)
            return;
        _textureCloneOps.RemoveAt(_textureCloneOps.Count - 1);
        RefreshCloneButtonsUi();
        ScheduleLivePreview();
    }

    private void ClearTextureCloneState()
    {
        _textureCloneOps.Clear();
        _cloneSourceNorm = null;
        _clonePickSourceNext = false;
        _cloneHoverNorm = null;
        _clonePainting = false;
        _cloneLastStampNorm = null;
        if (CloneStampModeToggle is not null)
            CloneStampModeToggle.IsChecked = false;
        _cloneStampMode = false;
        RefreshCloneButtonsUi();
        RefreshCloneOverlay();
    }

    private void RefreshCloneButtonsUi()
    {
        if (CloneUndoButton is not null)
            CloneUndoButton.IsEnabled = _textureCloneOps.Count > 0;
        if (CloneStampModeToggle is not null)
        {
            CloneStampModeToggle.Content = _textureCloneOps.Count > 0
                ? $"Klon ({_textureCloneOps.Count})"
                : "Klon";
        }
        if (ClonePickSourceButton is not null)
        {
            if (_clonePickSourceNext)
                ClonePickSourceButton.Content = "Kaynak…";
            else if (_cloneSourceNorm is null)
                ClonePickSourceButton.Content = "Kaynak al";
            else
                ClonePickSourceButton.Content = "Yeniden kaynak";
        }
        if (CloneStatusHint is not null)
        {
            if (!IsCloneStampMode)
                CloneStatusHint.Text = "";
            else if (_cloneSourceNorm is null || _clonePickSourceNext)
                CloneStatusHint.Text = "→ Kaynak seç (sol tık)";
            else
                CloneStatusHint.Text = "→ Hedefe tıkla / sürükle · sağ tık = yeni kaynak";
        }
        if (ResetAllPhotoEditsButton is not null)
            ResetAllPhotoEditsButton.IsEnabled = HasAnyPhotoEdits();
    }

    private void SetCloneSource(Point canvasNorm)
    {
        // Şablon tuvali uzayı — tamamlanan (siyah/beyaz) alanlara da klonlanabilir
        _cloneSourceNorm = new Point(
            Math.Clamp(canvasNorm.X, 0, 1),
            Math.Clamp(canvasNorm.Y, 0, 1));
        _clonePickSourceNext = false;
        _cloneLastStampNorm = null;
        RefreshCloneButtonsUi();
        RefreshCloneOverlay();
        UpdateCropUi();
    }

    private void StampCloneAt(Point canvasDestNorm, bool deferPreview)
    {
        if (_cloneSourceNorm is null)
            return;

        double dx = Math.Clamp(canvasDestNorm.X, 0, 1);
        double dy = Math.Clamp(canvasDestNorm.Y, 0, 1);
        var src = _cloneSourceNorm.Value;
        double radius = GetCloneRadiusNorm();

        // Aynı noktaya aşırı sık damgayı atla (sürüklerken)
        if (_cloneLastStampNorm is { } last)
        {
            double ddx = dx - last.X;
            double ddy = dy - last.Y;
            double minDist = radius * 0.35;
            if (ddx * ddx + ddy * ddy < minDist * minDist)
                return;
        }

        _textureCloneOps.Add(new TextureCloneOp(
            new NormalizedPoint(src.X, src.Y),
            new NormalizedPoint(dx, dy),
            radius,
            GetEffectiveCloneStampShape()));
        _cloneLastStampNorm = new Point(dx, dy);
        RefreshCloneButtonsUi();
        RefreshCloneOverlay();
        if (!deferPreview)
            ScheduleLivePreview();
    }

    private void RefreshCloneOverlay()
    {
        if (PreviewCloneCanvas is null)
            return;

        PreviewCloneCanvas.Children.Clear();
        bool showClone = IsCloneStampMode || _cloneSourceNorm is not null;
        // Şekil seçimi (pin/filigram fırça olmadan da) + fırça hover
        bool showFiligram = !IsCloneStampMode && (
            (IsFiligramBrushMode && (_filigramHoverNorm is not null || _filigramBrushCenterCanvas is not null))
            || IsPinShapedSelectionMode
            || (_filigramBrushCenterCanvas is not null
                && GetSelectedCloneBrushShape() != TextureCloneBrushShape.Normal));
        if (!showClone && !showFiligram)
            return;
        if (!TryGetLetterboxMapping(out _, out var rw, out var rh, out var ox, out var oy))
            return;
        if (PreviewCloneCanvas.Parent is not UIElement parent || LivePreviewImage is null)
            return;

        var origin = LivePreviewImage.TranslatePoint(new Point(0, 0), parent);
        // Zoom/letterbox: yarıçapı ekran pikseline çevir — aksi halde damga seçimden büyük görünür
        double radiusPx = Math.Max(2, GetCloneRadiusNorm() * Math.Min(rw, rh));
        bool filigramContext = showFiligram && !IsCloneStampMode;
        var brushShape = filigramContext
            ? GetEffectiveFiligramBrushShape()
            : GetEffectiveCloneStampShape();
        bool isNormalMarquee = GetSelectedCloneBrushShape() == TextureCloneBrushShape.Normal && filigramContext;
        double brushH = brushShape == TextureCloneBrushShape.Ellipse ? radiusPx * 2 * 0.62 : radiusPx * 2;
        double brushW = radiusPx * 2;
        double thinStroke = isNormalMarquee ? 1.15 : 1.35;

        void AddBrushRing(Point n, Color stroke, Color fill, double thickness)
        {
            double cx = origin.X + ox + n.X * rw;
            double cy = origin.Y + oy + n.Y * rh;
            var strokeBrush = new SolidColorBrush(stroke);
            var fillBrush = new SolidColorBrush(isNormalMarquee
                ? Color.FromArgb(18, stroke.R, stroke.G, stroke.B)
                : fill);

            System.Windows.Shapes.Shape ring = brushShape switch
            {
                TextureCloneBrushShape.Square or TextureCloneBrushShape.Normal => new System.Windows.Shapes.Rectangle
                {
                    Width = brushW,
                    Height = brushW,
                    Stroke = strokeBrush,
                    StrokeThickness = thickness,
                    StrokeDashArray = isNormalMarquee ? new DoubleCollection { 4, 2.5 } : null,
                    Fill = fillBrush,
                    IsHitTestVisible = false
                },
                TextureCloneBrushShape.SoftSquare => new System.Windows.Shapes.Rectangle
                {
                    Width = brushW,
                    Height = brushW,
                    RadiusX = radiusPx * 0.22,
                    RadiusY = radiusPx * 0.22,
                    Stroke = strokeBrush,
                    StrokeThickness = thickness,
                    Fill = fillBrush,
                    IsHitTestVisible = false
                },
                TextureCloneBrushShape.Ellipse => new System.Windows.Shapes.Ellipse
                {
                    Width = brushW,
                    Height = brushH,
                    Stroke = strokeBrush,
                    StrokeThickness = thickness,
                    Fill = fillBrush,
                    IsHitTestVisible = false
                },
                _ => new System.Windows.Shapes.Ellipse
                {
                    Width = brushW,
                    Height = brushW,
                    Stroke = strokeBrush,
                    StrokeThickness = thickness,
                    Fill = fillBrush,
                    IsHitTestVisible = false
                }
            };
            Canvas.SetLeft(ring, cx - brushW / 2);
            Canvas.SetTop(ring, cy - (brushShape == TextureCloneBrushShape.Ellipse ? brushH / 2 : brushW / 2));
            PreviewCloneCanvas.Children.Add(ring);

            // İnce merkez artı (nokta yerine daha net)
            double cross = Math.Max(4, Math.Min(8, radiusPx * 0.18));
            var hLine = new System.Windows.Shapes.Line
            {
                X1 = cx - cross, Y1 = cy, X2 = cx + cross, Y2 = cy,
                Stroke = strokeBrush, StrokeThickness = 1, IsHitTestVisible = false
            };
            var vLine = new System.Windows.Shapes.Line
            {
                X1 = cx, Y1 = cy - cross, X2 = cx, Y2 = cy + cross,
                Stroke = strokeBrush, StrokeThickness = 1, IsHitTestVisible = false
            };
            PreviewCloneCanvas.Children.Add(hLine);
            PreviewCloneCanvas.Children.Add(vLine);
        }

        // Filigram fırça (mor) — şekil + boyut
        if (showFiligram && !IsCloneStampMode)
        {
            Point? filigramPt = _filigramHoverNorm
                               ?? _filigramBrushCenterCanvas
                               ?? (_selectionPins.Count == 1 ? _selectionPins[0] : null);
            if (filigramPt is { } fp)
            {
                AddBrushRing(fp,
                    Color.FromArgb(235, 57, 255, 20),
                    Color.FromArgb(32, 57, 255, 20),
                    thinStroke);
            }
        }

        if (_cloneHoverNorm is { } onlyHover && IsCloneStampMode && (_cloneSourceNorm is null || _clonePickSourceNext))
        {
            AddBrushRing(onlyHover,
                Color.FromArgb(230, 40, 180, 70),
                Color.FromArgb(28, 40, 180, 70),
                thinStroke);
                // Label eklemiyoruz; sadece şekil/çerçeve göstergesi yeterli
            return;
        }

        if (_cloneHoverNorm is { } bc && IsCloneStampMode && _cloneSourceNorm is not null && !_clonePickSourceNext)
        {
            AddBrushRing(bc,
                Color.FromArgb(230, 255, 140, 0),
                Color.FromArgb(28, 255, 140, 0),
                thinStroke);
        }
    }

    private bool IsPinSelectMode => _pinSelectMode || PinSelectModeToggle?.IsChecked == true;

    private void PinSelectModeToggle_Changed(object sender, RoutedEventArgs e)
    {
        _pinSelectMode = PinSelectModeToggle?.IsChecked == true;
        if (_pinSelectMode)
        {
            if (CloneStampModeToggle?.IsChecked == true)
                CloneStampModeToggle.IsChecked = false;
            if (FiligramBrushModeToggle?.IsChecked == true)
                FiligramBrushModeToggle.IsChecked = false;
            CancelCropDrag();
            // Beyaz seçim varsa koru (gizli kalır); pin eklenince üzerine yazılır
            if (LivePreviewImage.Visibility == Visibility.Visible)
                LivePreviewImage.Cursor = Cursors.Pen;
        }
        else if (_eyedropperColorField is null)
        {
            LivePreviewImage.Cursor = Cursors.Cross;
        }

        RefreshPinButtonsUi();
        RefreshSelectionOverlaysAfterModeChange();
    }

    private void PinUndoButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectionPins.Count == 0)
            return;
        _selectionPins.RemoveAt(_selectionPins.Count - 1);
        ApplyPinsToPendingSelection();
        RefreshPinOverlay();
        RefreshPinButtonsUi();
        UpdateCropUi();
    }

    private void PinClearButton_Click(object sender, RoutedEventArgs e)
    {
        RememberClearedSelection();
        ClearSelectionPins();
        _filigramBrushCenterCanvas = null;
        _filigramHoverNorm = null;
        _pendingCropRect = null;
        SetCropOverlay(null);
        RefreshCloneOverlay();
        UpdateCropUi();
    }

    private void ClearSelectionPins()
    {
        _selectionPins.Clear();
        RefreshPinOverlay();
        RefreshPinButtonsUi();
        RefreshCloneOverlay();
    }

    private void RefreshPinButtonsUi()
    {
        if (PinUndoButton is not null)
            PinUndoButton.IsEnabled = _selectionPins.Count > 0;
        if (PinClearButton is not null)
            PinClearButton.IsEnabled = _selectionPins.Count > 0;
        if (PinSelectModeToggle is not null && _selectionPins.Count > 0)
            PinSelectModeToggle.Content = $"📌 Pin ({_selectionPins.Count})";
        else if (PinSelectModeToggle is not null)
            PinSelectModeToggle.Content = "📌 Pin seçim";
    }

    /// <summary>
    /// Pin / klon / filigram / kaynak al açıkken beyaz kırp çerçevesi gizlenir.
    /// Serbest seçimde (hiçbiri açık değilken) beyaz alan aktif olur.
    /// </summary>
    private bool IsToolSelectionModeActive =>
        IsPinSelectMode || IsCloneStampMode || IsFiligramBrushMode || _clonePickSourceNext;

    /// <summary>
    /// Pin + tek nokta + N dışı şekil: yalnızca yeşil şekil halkası.
    /// </summary>
    private bool IsPinShapedSelectionMode =>
        IsPinSelectMode
        && _selectionPins.Count == 1
        && GetSelectedCloneBrushShape() != TextureCloneBrushShape.Normal;

    private bool ShouldShowPendingCropOverlayVisual =>
        _pendingCropRect is not null && !IsToolSelectionModeActive;

    private bool IsFreeMarqueeMode =>
        !IsToolSelectionModeActive && _eyedropperColorField is null;

    private void ClearPendingWhiteSelection()
    {
        if (_pendingCropRect is not null || _selectionPins.Count > 0 || _filigramBrushCenterCanvas is not null)
            RememberClearedSelection();
        _pendingCropRect = null;
        CancelCropDrag();
        SetCropOverlay(null);
        UpdateCropUi();
    }

    private void RefreshSelectionOverlaysAfterModeChange()
    {
        // Beyaz çerçeve: yalnızca serbest seçimde
        SetCropOverlay(ShouldShowPendingCropOverlayVisual ? _pendingCropRect : null);
        RefreshPinOverlay();
        RefreshCloneOverlay();
        UpdateCropUi();
    }

    private void ApplyPinsToPendingSelection()
    {
        if (_selectionPins.Count == 0)
            return;

        bool shaped = GetSelectedCloneBrushShape() != TextureCloneBrushShape.Normal;

        if (_selectionPins.Count == 1)
        {
            var p = _selectionPins[0];
            if (!shaped)
            {
                // N (şekil yok): tek pin henüz seçim oluşturmaz — sadece nokta
                // Böylece peş peşe yakın pin konabilir
                _pendingCropRect = null;
                _filigramBrushCenterCanvas = null;
                SetCropOverlay(null);
                RefreshCloneOverlay();
                return;
            }

            // Şekil seçili: tek pin = şekil+boyut alanı
            double r = GetCloneRadiusNorm();
            double halfX = Math.Clamp(r, 0.002, 0.35);
            double halfY = GetEffectiveFiligramBrushShape() == TextureCloneBrushShape.Ellipse
                ? halfX * 0.62
                : halfX;
            _pendingCropRect = ClampNormRect(p.X - halfX, p.Y - halfY, halfX * 2, halfY * 2);
            _filigramBrushCenterCanvas = p;
            SetCropOverlay(ShouldShowPendingCropOverlayVisual ? _pendingCropRect : null);
            RefreshCloneOverlay();
            return;
        }

        // 2+ pin: seçim şekli pinlere göre (bbox / çokgen)
        _filigramBrushCenterCanvas = null;
        double minX = _selectionPins.Min(p => p.X);
        double minY = _selectionPins.Min(p => p.Y);
        double maxX = _selectionPins.Max(p => p.X);
        double maxY = _selectionPins.Max(p => p.Y);
        _pendingCropRect = ClampNormRect(minX, minY, Math.Max(0.002, maxX - minX), Math.Max(0.002, maxY - minY));
        SetCropOverlay(ShouldShowPendingCropOverlayVisual ? _pendingCropRect : null);
        RefreshCloneOverlay();
    }

    private bool TryGetSelectionCenterNorm(out Point center)
    {
        if (_selectionPins.Count == 1)
        {
            center = _selectionPins[0];
            return true;
        }

        if (_selectionPins.Count >= 2)
        {
            center = new Point(
                _selectionPins.Average(p => p.X),
                _selectionPins.Average(p => p.Y));
            return true;
        }

        if (_filigramBrushCenterCanvas is { } brush)
        {
            center = brush;
            return true;
        }

        if (_pendingCropRect is { } r)
        {
            center = new Point(r.Left + r.Width / 2, r.Top + r.Height / 2);
            return true;
        }

        center = default;
        return false;
    }

    private bool ShouldDrivePinSelection =>
        IsPinSelectMode && _selectionPins.Count > 0;

    private void SyncPinsFromPendingRect(double? moveDx = null, double? moveDy = null)
    {
        if (_selectionPins.Count == 0 || _pendingCropRect is null)
            return;

        if (_selectionPins.Count == 1)
        {
            var r = _pendingCropRect;
            var c = new Point(r.Left + r.Width / 2, r.Top + r.Height / 2);
            _selectionPins[0] = c;
            _filigramBrushCenterCanvas = c;
            double half = Math.Min(r.Width, r.Height) / 2.0;
            if (CloneBrushSizeSlider is not null && !_updatingBrushFromPin)
            {
                _updatingBrushFromPin = true;
                CloneBrushSizeSlider.Value = Math.Clamp(half * 100.0, CloneBrushSizeSlider.Minimum, CloneBrushSizeSlider.Maximum);
                _updatingBrushFromPin = false;
            }
            RefreshPinOverlay();
            RefreshCloneOverlay();
            return;
        }

        if (moveDx is double dx && moveDy is double dy && _pinsAtCropDragStart is { Count: > 0 })
        {
            for (int i = 0; i < _selectionPins.Count && i < _pinsAtCropDragStart.Count; i++)
            {
                var o = _pinsAtCropDragStart[i];
                _selectionPins[i] = new Point(
                    Math.Clamp(o.X + dx, 0, 1),
                    Math.Clamp(o.Y + dy, 0, 1));
            }
            RefreshPinOverlay();
            return;
        }

        // Resize / aspect: pinleri eski bbox'tan yeni bbox'a map et
        if (_cropRectAtDragStart is { } oldR && oldR.Width > 1e-6 && oldR.Height > 1e-6
            && _pinsAtCropDragStart is { Count: > 0 })
        {
            var n = _pendingCropRect;
            for (int i = 0; i < _selectionPins.Count && i < _pinsAtCropDragStart.Count; i++)
            {
                var o = _pinsAtCropDragStart[i];
                double ux = (o.X - oldR.Left) / oldR.Width;
                double uy = (o.Y - oldR.Top) / oldR.Height;
                _selectionPins[i] = new Point(
                    Math.Clamp(n.Left + ux * n.Width, 0, 1),
                    Math.Clamp(n.Top + uy * n.Height, 0, 1));
            }
            RefreshPinOverlay();
            return;
        }

        ApplyPinsToPendingSelection();
    }

    private void RefreshPinOverlay()
    {
        if (PreviewPinCanvas is null)
            return;

        PreviewPinCanvas.Children.Clear();
        if (_selectionPins.Count == 0)
            return;
        if (!TryGetLetterboxMapping(out _, out var rw, out var rh, out var ox, out var oy))
            return;
        if (PreviewPinCanvas.Parent is not UIElement parent)
            return;

        var origin = LivePreviewImage.TranslatePoint(new Point(0, 0), parent);
        var screenPts = new PointCollection();
        foreach (var p in _selectionPins)
            screenPts.Add(new Point(origin.X + ox + p.X * rw, origin.Y + oy + p.Y * rh));

        // 2 pin: en kısa yol (doğru parçası) kalın çizgi
        // 3+ pin: kapalı çokgen (pin sırası + son→ilk)
        if (_selectionPins.Count == 2)
        {
            var line = new System.Windows.Shapes.Line
            {
                X1 = screenPts[0].X,
                Y1 = screenPts[0].Y,
                X2 = screenPts[1].X,
                Y2 = screenPts[1].Y,
                Stroke = new SolidColorBrush(Color.FromArgb(200, 255, 59, 48)),
                StrokeThickness = 4,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                IsHitTestVisible = false
            };
            PreviewPinCanvas.Children.Add(line);
        }
        else if (_selectionPins.Count >= 3)
        {
            var fill = new Polygon
            {
                Points = screenPts,
                Fill = new SolidColorBrush(Color.FromArgb(55, 255, 59, 48)),
                Stroke = new SolidColorBrush(Color.FromArgb(220, 255, 59, 48)),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 3, 2 },
                IsHitTestVisible = false
            };
            PreviewPinCanvas.Children.Add(fill);
        }
        else if (_selectionPins.Count >= 2)
        {
            // yedek polyline
            var pl = new System.Windows.Shapes.Polyline
            {
                Points = screenPts,
                Stroke = new SolidColorBrush(Color.FromArgb(200, 255, 59, 48)),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 3, 2 },
                IsHitTestVisible = false
            };
            PreviewPinCanvas.Children.Add(pl);
        }

        // Pin noktaları (üste)
        for (int i = 0; i < _selectionPins.Count; i++)
        {
            double cx = screenPts[i].X;
            double cy = screenPts[i].Y;

            var outer = new Ellipse
            {
                Width = 14,
                Height = 14,
                Fill = new SolidColorBrush(Color.FromArgb(230, 255, 59, 48)),
                Stroke = Brushes.White,
                StrokeThickness = 1.5,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(outer, cx - 7);
            Canvas.SetTop(outer, cy - 7);
            PreviewPinCanvas.Children.Add(outer);

            var label = new TextBlock
            {
                Text = (i + 1).ToString(),
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(label, cx - (i + 1 >= 10 ? 5.5 : 3.5));
            Canvas.SetTop(label, cy - 7);
            PreviewPinCanvas.Children.Add(label);
        }
    }

    private void PreviewZoomIn_Click(object sender, RoutedEventArgs e) =>
        ZoomPreviewBy(PreviewZoomStep);

    private void PreviewZoomOut_Click(object sender, RoutedEventArgs e) =>
        ZoomPreviewBy(1.0 / PreviewZoomStep);

    private void PreviewZoomReset_Click(object sender, RoutedEventArgs e) =>
        ResetPreviewZoom();

    private void ZoomPreviewBy(double factor)
    {
        Point? center = null;
        if (PreviewScrollViewer is not null
            && PreviewScrollViewer.ViewportWidth > 1
            && PreviewScrollViewer.ViewportHeight > 1)
        {
            center = new Point(
                PreviewScrollViewer.ViewportWidth / 2,
                PreviewScrollViewer.ViewportHeight / 2);
        }
        ApplyPreviewZoom(_previewZoom * factor, center);
    }

    private void ResetPreviewZoom() => ApplyPreviewZoom(1.0, null);

    private void PreviewScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Yapıştırma aktifken tekerlek = döndür (Ctrl ile zoom)
        if (_floatingPasteActive && Keyboard.Modifiers != ModifierKeys.Control)
        {
            e.Handled = true;
            double step = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift ? 15 : 5;
            NudgeFloatingPasteRotation(e.Delta > 0 ? -step : step);
            return;
        }

        if (Keyboard.Modifiers != ModifierKeys.Control)
            return;

        e.Handled = true;
        if (PreviewScrollViewer is null)
            return;

        var pos = e.GetPosition(PreviewScrollViewer);
        double factor = e.Delta > 0 ? PreviewZoomStep : 1.0 / PreviewZoomStep;
        ApplyPreviewZoom(_previewZoom * factor, pos);
    }

    private void PreviewScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_updatingZoomHost)
            return;
        // Küçük toolbar reflow (Kaynak al yazısı vb.) titreme yaratmasın
        if (e.PreviousSize.Width > 1 && e.PreviousSize.Height > 1
            && Math.Abs(e.NewSize.Width - e.PreviousSize.Width) < 1.5
            && Math.Abs(e.NewSize.Height - e.PreviousSize.Height) < 1.5)
            return;

        UpdatePreviewZoomHostSize();
        Dispatcher.BeginInvoke(RefreshPendingCropOverlay, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void ApplyPreviewZoom(double newZoom, Point? zoomCenterInViewport)
    {
        newZoom = Math.Clamp(newZoom, PreviewZoomMin, PreviewZoomMax);
        if (Math.Abs(newZoom - 1.0) < 0.02)
            newZoom = 1.0;

        var sv = PreviewScrollViewer;
        if (sv is null)
        {
            _previewZoom = newZoom;
            RefreshPreviewZoomUi();
            return;
        }

        double oldZoom = Math.Max(0.01, _previewZoom);
        Point? contentPoint = null;
        if (zoomCenterInViewport is { } vp)
            contentPoint = new Point(sv.HorizontalOffset + vp.X, sv.VerticalOffset + vp.Y);

        _previewZoom = newZoom;
        UpdatePreviewZoomHostSize();
        sv.UpdateLayout();

        if (newZoom <= 1.001)
        {
            sv.ScrollToHorizontalOffset(0);
            sv.ScrollToVerticalOffset(0);
        }
        else if (contentPoint is { } cp && zoomCenterInViewport is { } vp2)
        {
            double scale = newZoom / oldZoom;
            double targetX = cp.X * scale - vp2.X;
            double targetY = cp.Y * scale - vp2.Y;
            sv.ScrollToHorizontalOffset(Math.Max(0, targetX));
            sv.ScrollToVerticalOffset(Math.Max(0, targetY));
        }

        RefreshPreviewZoomUi();
        Dispatcher.BeginInvoke(RefreshPendingCropOverlay, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void UpdatePreviewZoomHostSize()
    {
        if (PreviewScrollViewer is null || PreviewZoomHost is null)
            return;
        if (_updatingZoomHost)
            return;

        double vw = PreviewScrollViewer.ViewportWidth;
        double vh = PreviewScrollViewer.ViewportHeight;
        if (vw < 2 || vh < 2)
        {
            vw = Math.Max(vw, PreviewScrollViewer.ActualWidth);
            vh = Math.Max(vh, PreviewScrollViewer.ActualHeight);
        }
        if (vw < 2 || vh < 2)
            return;

        double newW = vw * _previewZoom;
        double newH = vh * _previewZoom;

        // Aynı boyut → layout thrash / titreme olmasın
        if (Math.Abs(PreviewZoomHost.Width - newW) < 0.5
            && Math.Abs(PreviewZoomHost.Height - newH) < 0.5)
            return;

        var sv = PreviewScrollViewer;
        double oldW = PreviewZoomHost.ActualWidth > 1 ? PreviewZoomHost.ActualWidth : PreviewZoomHost.Width;
        double oldH = PreviewZoomHost.ActualHeight > 1 ? PreviewZoomHost.ActualHeight : PreviewZoomHost.Height;
        if (oldW < 2) oldW = PreviewZoomHost.Width;
        if (oldH < 2) oldH = PreviewZoomHost.Height;

        double contentX = sv.HorizontalOffset + sv.ViewportWidth / 2.0;
        double contentY = sv.VerticalOffset + sv.ViewportHeight / 2.0;

        _updatingZoomHost = true;
        try
        {
            PreviewZoomHost.Width = newW;
            PreviewZoomHost.Height = newH;
            sv.UpdateLayout();

            if (_previewZoom <= 1.001)
            {
                sv.ScrollToHorizontalOffset(0);
                sv.ScrollToVerticalOffset(0);
                return;
            }

            if (oldW > 2 && oldH > 2)
            {
                double scaleX = newW / oldW;
                double scaleY = newH / oldH;
                double targetX = contentX * scaleX - sv.ViewportWidth / 2.0;
                double targetY = contentY * scaleY - sv.ViewportHeight / 2.0;
                sv.ScrollToHorizontalOffset(Math.Max(0, targetX));
                sv.ScrollToVerticalOffset(Math.Max(0, targetY));
            }
        }
        finally
        {
            _updatingZoomHost = false;
        }
    }

    private void RefreshPreviewZoomUi()
    {
        string label = $"{(int)Math.Round(_previewZoom * 100)}%";
        if (PreviewZoomResetButton is not null)
            PreviewZoomResetButton.Content = label;
        if (PreviewZoomInButton is not null)
            PreviewZoomInButton.IsEnabled = _previewZoom < PreviewZoomMax - 0.001;
        if (PreviewZoomOutButton is not null)
            PreviewZoomOutButton.IsEnabled = _previewZoom > PreviewZoomMin + 0.001;
    }

    private bool TryGetLetterboxMapping(out BitmapSource bitmap, out double renderedW, out double renderedH, out double offsetX, out double offsetY)
    {
        bitmap = null!;
        renderedW = 0;
        renderedH = 0;
        offsetX = 0;
        offsetY = 0;

        if (LivePreviewImage.Source is not BitmapSource b)
            return false;

        if (LivePreviewImage.ActualWidth < 1 || LivePreviewImage.ActualHeight < 1)
            return false;

        bitmap = b;
        double sourceW = bitmap.Width;
        double sourceH = bitmap.Height;
        double scale = Math.Min(LivePreviewImage.ActualWidth / sourceW, LivePreviewImage.ActualHeight / sourceH);
        if (scale <= 0)
            return false;

        renderedW = sourceW * scale;
        renderedH = sourceH * scale;
        offsetX = (LivePreviewImage.ActualWidth - renderedW) / 2;
        offsetY = (LivePreviewImage.ActualHeight - renderedH) / 2;
        return renderedW > 0 && renderedH > 0;
    }

    private bool TryPointToNorm(Point p, out double nx, out double ny)
    {
        nx = 0;
        ny = 0;
        if (!TryGetLetterboxMapping(out _, out var renderedW, out var renderedH, out var offsetX, out var offsetY))
            return false;
        nx = Math.Clamp((p.X - offsetX) / renderedW, 0, 1);
        ny = Math.Clamp((p.Y - offsetY) / renderedH, 0, 1);
        return true;
    }

    private bool TryComputeNormRectFromPoints(Point a, Point b, out NormalizedCropRect rect)
    {
        rect = default!;
        if (!TryPointToNorm(a, out var x1, out var y1) || !TryPointToNorm(b, out var x2, out var y2))
            return false;

        double left = Math.Min(x1, x2);
        double top = Math.Min(y1, y2);
        double right = Math.Max(x1, x2);
        double bottom = Math.Max(y1, y2);
        double width = right - left;
        double height = bottom - top;
        if (width < 0.002 || height < 0.002)
            return false;

        rect = new NormalizedCropRect(left, top, width, height);
        return true;
    }

    private static NormalizedCropRect ClampNormRect(double left, double top, double width, double height)
    {
        width = Math.Clamp(width, 0.0005, 1);
        height = Math.Clamp(height, 0.0005, 1);
        left = Math.Clamp(left, 0, 1 - width);
        top = Math.Clamp(top, 0, 1 - height);
        return new NormalizedCropRect(left, top, width, height);
    }

    private void SetCropOverlay(NormalizedCropRect? rect)
    {
        if (PreviewCropSelectionRect is null)
            return;

        if (rect is null)
        {
            PreviewCropSelectionRect.Visibility = Visibility.Collapsed;
            return;
        }

        if (!TryGetLetterboxMapping(out _, out var renderedW, out var renderedH, out var offsetX, out var offsetY))
            return;

        if (PreviewCropSelectionRect.Parent is not UIElement parent)
            return;

        var origin = LivePreviewImage.TranslatePoint(new Point(0, 0), parent);
        double left = origin.X + offsetX + rect.Left * renderedW;
        double top = origin.Y + offsetY + rect.Top * renderedH;

        PreviewCropSelectionRect.Visibility = Visibility.Visible;
        PreviewCropSelectionRect.Margin = new Thickness(left, top, 0, 0);
        PreviewCropSelectionRect.Width = Math.Max(2, rect.Width * renderedW);
        PreviewCropSelectionRect.Height = Math.Max(2, rect.Height * renderedH);
    }

    private void RefreshPendingCropOverlay()
    {
        SetCropOverlay(ShouldShowPendingCropOverlayVisual ? _pendingCropRect : null);
        RefreshPinOverlay();
        if (IsPinSelectMode && _selectionPins.Count == 1)
            RefreshCloneOverlay();
    }

    private void SyncCropPxBoxesFromPending()
    {
        if (_updatingCropPxUi)
            return;
        _updatingCropPxUi = true;
        try
        {
            if (_pendingCropRect is null || _previewPixelWidth < 1 || _previewPixelHeight < 1)
            {
                if (CropWidthPxBox is not null) CropWidthPxBox.Text = "";
                if (CropHeightPxBox is not null) CropHeightPxBox.Text = "";
                return;
            }

            int w = Math.Max(1, (int)Math.Round(_pendingCropRect.Width * _previewPixelWidth));
            int h = Math.Max(1, (int)Math.Round(_pendingCropRect.Height * _previewPixelHeight));
            if (CropWidthPxBox is not null) CropWidthPxBox.Text = w.ToString();
            if (CropHeightPxBox is not null) CropHeightPxBox.Text = h.ToString();
        }
        finally
        {
            _updatingCropPxUi = false;
        }
    }

    private void ApplyCropPxFromBoxes()
    {
        if (_updatingCropPxUi || _previewPixelWidth < 1 || _previewPixelHeight < 1)
            return;
        if (!int.TryParse(CropWidthPxBox?.Text?.Trim(), out int w) || w < 1)
            return;
        if (!int.TryParse(CropHeightPxBox?.Text?.Trim(), out int h) || h < 1)
            return;

        w = Math.Min(w, _previewPixelWidth);
        h = Math.Min(h, _previewPixelHeight);
        double nw = w / (double)_previewPixelWidth;
        double nh = h / (double)_previewPixelHeight;

        double left;
        double top;
        if (_pendingCropRect is { } cur)
        {
            left = cur.Left + (cur.Width - nw) / 2;
            top = cur.Top + (cur.Height - nh) / 2;
        }
        else
        {
            left = (1 - nw) / 2;
            top = (1 - nh) / 2;
        }

        _pendingCropRect = ClampNormRect(left, top, nw, nh);
        RefreshPendingCropOverlay();
        UpdateCropUi();
    }

    private void CropPxBox_LostFocus(object sender, RoutedEventArgs e) => ApplyCropPxFromBoxes();

    private void CropPxBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyCropPxFromBoxes();
            e.Handled = true;
        }
    }

    private void CropAlignLeft_Click(object sender, RoutedEventArgs e) => NudgePendingPosition(-1, 0);
    private void CropAlignCenterH_Click(object sender, RoutedEventArgs e) => AlignPending(h: "center");
    private void CropAlignRight_Click(object sender, RoutedEventArgs e) => NudgePendingPosition(1, 0);
    private void CropAlignTop_Click(object sender, RoutedEventArgs e) => NudgePendingPosition(0, -1);
    private void CropAlignCenterV_Click(object sender, RoutedEventArgs e) => AlignPending(v: "center");
    private void CropAlignBottom_Click(object sender, RoutedEventArgs e) => NudgePendingPosition(0, 1);

    private void CropShrink_Click(object sender, RoutedEventArgs e) => NudgePendingSize(-1, -1);
    private void CropGrow_Click(object sender, RoutedEventArgs e) => NudgePendingSize(1, 1);
    private void CropShrinkWidth_Click(object sender, RoutedEventArgs e) => NudgePendingSize(-1, 0);
    private void CropGrowWidth_Click(object sender, RoutedEventArgs e) => NudgePendingSize(1, 0);
    private void CropShrinkHeight_Click(object sender, RoutedEventArgs e) => NudgePendingSize(0, -1);
    private void CropGrowHeight_Click(object sender, RoutedEventArgs e) => NudgePendingSize(0, 1);

    private void CropAspect43_Click(object sender, RoutedEventArgs e) => FitPendingToAspect(4, 3);
    private void CropAspect44_Click(object sender, RoutedEventArgs e) => FitPendingToAspect(4, 4);
    private void CropAspect45_Click(object sender, RoutedEventArgs e) => FitPendingToAspect(4, 5);

    private void EnsurePendingCropExists()
    {
        if (_pendingCropRect is not null)
            return;
        if (ShouldDrivePinSelection)
        {
            ApplyPinsToPendingSelection();
            if (_pendingCropRect is not null)
                return;
        }
        // Varsayılan: ortada %60×%60 seçim
        _pendingCropRect = ClampNormRect(0.2, 0.2, 0.6, 0.6);
    }

    /// <summary>
    /// Yakınlaştırmada 1 adım ≈ 1 ekran pikseli (hassas ayar).
    /// </summary>
    private double GetZoomAwarePixelStep(int steps)
    {
        double zoom = Math.Max(1.0, _previewZoom);
        return steps / zoom;
    }

    /// <summary>
    /// Şekil en-boy butonları için fırça boyutu — yakınlaştırmada 0.1% adım.
    /// </summary>
    private void AdjustCloneBrushSizeFine(int direction)
    {
        if (CloneBrushSizeSlider is null)
            return;

        double zoom = Math.Max(1.0, _previewZoom);
        double step = zoom >= 2.0 ? 0.1 : zoom >= 1.25 ? 0.2 : 0.5;
        double v = CloneBrushSizeSlider.Value;
        double next = direction < 0 ? v - step : v + step;
        next = Math.Round(next, 1);
        CloneBrushSizeSlider.Value = Math.Clamp(next, CloneBrushSizeSlider.Minimum, CloneBrushSizeSlider.Maximum);
    }

    private void NudgePinSelectionPosition(int deltaXPx, int deltaYPx)
    {
        if (_selectionPins.Count == 0)
            return;

        int baseW = Math.Max(1, _previewPixelWidth);
        int baseH = Math.Max(1, _previewPixelHeight);
        double ndx = GetZoomAwarePixelStep(deltaXPx) / baseW;
        double ndy = GetZoomAwarePixelStep(deltaYPx) / baseH;
        for (int i = 0; i < _selectionPins.Count; i++)
        {
            var p = _selectionPins[i];
            _selectionPins[i] = new Point(
                Math.Clamp(p.X + ndx, 0, 1),
                Math.Clamp(p.Y + ndy, 0, 1));
        }
        ApplyPinsToPendingSelection();
        RefreshPinOverlay();
        UpdateCropUi();
    }

    private void NudgePinSelectionSize(int deltaWidthPx, int deltaHeightPx)
    {
        if (_selectionPins.Count == 1)
        {
            int delta = deltaWidthPx != 0 ? deltaWidthPx : deltaHeightPx;
            if (delta == 0)
                return;
            AdjustCloneBrushSizeFine(delta > 0 ? +1 : -1);
            ApplyPinsToPendingSelection();
            RefreshPinOverlay();
            UpdateCropUi();
            return;
        }

        if (_selectionPins.Count < 2 || _pendingCropRect is null)
            return;

        var old = _pendingCropRect;
        NudgePendingSizeCore(deltaWidthPx, deltaHeightPx);
        if (_pendingCropRect is null || old.Width < 1e-6 || old.Height < 1e-6)
            return;

        double cx = old.Left + old.Width / 2;
        double cy = old.Top + old.Height / 2;
        double sx = _pendingCropRect.Width / old.Width;
        double sy = _pendingCropRect.Height / old.Height;
        for (int i = 0; i < _selectionPins.Count; i++)
        {
            var p = _selectionPins[i];
            _selectionPins[i] = new Point(
                Math.Clamp(cx + (p.X - cx) * sx, 0, 1),
                Math.Clamp(cy + (p.Y - cy) * sy, 0, 1));
        }
        ApplyPinsToPendingSelection();
        RefreshPinOverlay();
        UpdateCropUi();
    }

    private void NudgePendingSizeCore(int deltaWidthPx, int deltaHeightPx)
    {
        if (_pendingCropRect is null)
            return;

        int baseW = Math.Max(1, _previewPixelWidth);
        int baseH = Math.Max(1, _previewPixelHeight);
        var r = _pendingCropRect;
        double dW = GetZoomAwarePixelStep(deltaWidthPx);
        double dH = GetZoomAwarePixelStep(deltaHeightPx);
        double curW = r.Width * baseW;
        double curH = r.Height * baseH;
        double newW = Math.Clamp(curW + dW, 0.5, baseW);
        double newH = Math.Clamp(curH + dH, 0.5, baseH);
        double nw = newW / baseW;
        double nh = newH / baseH;
        double left = r.Left + (r.Width - nw) / 2;
        double top = r.Top + (r.Height - nh) / 2;
        _pendingCropRect = ClampNormRect(left, top, nw, nh);
    }

    private void FitPendingToAspect(int ratioW, int ratioH)
    {
        int baseW = Math.Max(1, _previewPixelWidth);
        int baseH = Math.Max(1, _previewPixelHeight);
        if (ratioW < 1 || ratioH < 1)
            return;

        double targetAr = ratioW / (double)ratioH;
        double imageAr = baseW / (double)baseH;

        int cropW;
        int cropH;
        if (imageAr > targetAr)
        {
            // Gorsel daha genis -> yuksekligi doldur
            cropH = baseH;
            cropW = Math.Clamp((int)Math.Round(baseH * targetAr), 1, baseW);
        }
        else
        {
            // Gorsel daha dik -> genisligi doldur
            cropW = baseW;
            cropH = Math.Clamp((int)Math.Round(baseW / targetAr), 1, baseH);
        }

        double nw = cropW / (double)baseW;
        double nh = cropH / (double)baseH;
        double left = (1.0 - nw) / 2.0;
        double top = (1.0 - nh) / 2.0;
        _pendingCropRect = ClampNormRect(left, top, nw, nh);
        if (ShouldDrivePinSelection)
        {
            if (_selectionPins.Count == 1)
            {
                SyncPinsFromPendingRect();
            }
            else if (_selectionPins.Count >= 2)
            {
                var oldPins = _selectionPins.ToList();
                double minX = oldPins.Min(p => p.X);
                double minY = oldPins.Min(p => p.Y);
                double maxX = oldPins.Max(p => p.X);
                double maxY = oldPins.Max(p => p.Y);
                double ow = Math.Max(1e-6, maxX - minX);
                double oh = Math.Max(1e-6, maxY - minY);
                for (int i = 0; i < _selectionPins.Count; i++)
                {
                    var o = oldPins[i];
                    double ux = (o.X - minX) / ow;
                    double uy = (o.Y - minY) / oh;
                    _selectionPins[i] = new Point(
                        Math.Clamp(left + ux * nw, 0, 1),
                        Math.Clamp(top + uy * nh, 0, 1));
                }
                RefreshPinOverlay();
            }
        }
        RefreshPendingCropOverlay();
        UpdateCropUi();
    }

    private void NudgePendingPosition(int deltaXPx, int deltaYPx)
    {
        if (ShouldDrivePinSelection)
        {
            NudgePinSelectionPosition(deltaXPx, deltaYPx);
            return;
        }

        EnsurePendingCropExists();
        if (_pendingCropRect is null)
            return;

        int baseW = Math.Max(1, _previewPixelWidth);
        int baseH = Math.Max(1, _previewPixelHeight);
        var r = _pendingCropRect;
        double left = r.Left + GetZoomAwarePixelStep(deltaXPx) / baseW;
        double top = r.Top + GetZoomAwarePixelStep(deltaYPx) / baseH;
        _pendingCropRect = ClampNormRect(left, top, r.Width, r.Height);
        RefreshPendingCropOverlay();
        UpdateCropUi();
    }

    private void ScalePending(double widthFactor, double heightFactor)
    {
        if (ShouldDrivePinSelection && _selectionPins.Count == 1)
        {
            // Tek pin: fırça boyutunu ölçekle
            double cur = CloneBrushSizeSlider?.Value ?? 4;
            double next = Math.Clamp(cur * ((widthFactor + heightFactor) / 2.0),
                CloneBrushSizeSlider?.Minimum ?? 0.1,
                CloneBrushSizeSlider?.Maximum ?? 40);
            if (CloneBrushSizeSlider is not null)
                CloneBrushSizeSlider.Value = next;
            ApplyPinsToPendingSelection();
            RefreshPinOverlay();
            UpdateCropUi();
            return;
        }

        EnsurePendingCropExists();
        if (_pendingCropRect is null)
            return;

        var old = _pendingCropRect;
        double nw = Math.Clamp(old.Width * widthFactor, 0.01, 1);
        double nh = Math.Clamp(old.Height * heightFactor, 0.01, 1);
        double left = old.Left + (old.Width - nw) / 2;
        double top = old.Top + (old.Height - nh) / 2;
        _pendingCropRect = ClampNormRect(left, top, nw, nh);

        if (ShouldDrivePinSelection && _selectionPins.Count >= 2
            && old.Width > 1e-6 && old.Height > 1e-6)
        {
            double cx = old.Left + old.Width / 2;
            double cy = old.Top + old.Height / 2;
            double sx = _pendingCropRect.Width / old.Width;
            double sy = _pendingCropRect.Height / old.Height;
            for (int i = 0; i < _selectionPins.Count; i++)
            {
                var p = _selectionPins[i];
                _selectionPins[i] = new Point(
                    Math.Clamp(cx + (p.X - cx) * sx, 0, 1),
                    Math.Clamp(cy + (p.Y - cy) * sy, 0, 1));
            }
            ApplyPinsToPendingSelection();
            RefreshPinOverlay();
        }
        else if (ShouldDrivePinSelection)
        {
            SyncPinsFromPendingRect();
        }

        RefreshPendingCropOverlay();
        UpdateCropUi();
    }

    private void NudgePendingSize(int deltaWidthPx, int deltaHeightPx)
    {
        if (ShouldDrivePinSelection)
        {
            NudgePinSelectionSize(deltaWidthPx, deltaHeightPx);
            return;
        }

        EnsurePendingCropExists();
        if (_pendingCropRect is null)
            return;

        NudgePendingSizeCore(deltaWidthPx, deltaHeightPx);
        RefreshPendingCropOverlay();
        UpdateCropUi();
    }

    private void AlignPending(string? h = null, string? v = null)
    {
        if (ShouldDrivePinSelection && _pendingCropRect is null)
            ApplyPinsToPendingSelection();
        if (_pendingCropRect is null)
            return;
        var r = _pendingCropRect;
        double left = r.Left;
        double top = r.Top;
        if (h == "left") left = 0;
        else if (h == "center") left = (1 - r.Width) / 2;
        else if (h == "right") left = 1 - r.Width;
        if (v == "top") top = 0;
        else if (v == "center") top = (1 - r.Height) / 2;
        else if (v == "bottom") top = 1 - r.Height;

        double dx = left - r.Left;
        double dy = top - r.Top;
        _pendingCropRect = ClampNormRect(left, top, r.Width, r.Height);
        if (ShouldDrivePinSelection)
        {
            for (int i = 0; i < _selectionPins.Count; i++)
            {
                var p = _selectionPins[i];
                _selectionPins[i] = new Point(
                    Math.Clamp(p.X + dx, 0, 1),
                    Math.Clamp(p.Y + dy, 0, 1));
            }
            if (_selectionPins.Count == 1)
                _filigramBrushCenterCanvas = _selectionPins[0];
            RefreshPinOverlay();
            RefreshCloneOverlay();
        }
        RefreshPendingCropOverlay();
        UpdateCropUi();
    }

    private string HitTestPendingHandle(Point pos)
    {
        if (_pendingCropRect is null || !TryGetLetterboxMapping(out _, out var rw, out var rh, out var ox, out var oy))
            return "";

        double l = ox + _pendingCropRect.Left * rw;
        double t = oy + _pendingCropRect.Top * rh;
        double r = l + _pendingCropRect.Width * rw;
        double b = t + _pendingCropRect.Height * rh;
        const double hs = 10;
        bool nearL = Math.Abs(pos.X - l) <= hs;
        bool nearR = Math.Abs(pos.X - r) <= hs;
        bool nearT = Math.Abs(pos.Y - t) <= hs;
        bool nearB = Math.Abs(pos.Y - b) <= hs;
        bool inX = pos.X >= l - hs && pos.X <= r + hs;
        bool inY = pos.Y >= t - hs && pos.Y <= b + hs;

        if (nearT && nearL) return "nw";
        if (nearT && nearR) return "ne";
        if (nearB && nearL) return "sw";
        if (nearB && nearR) return "se";
        if (nearT && inX) return "n";
        if (nearB && inX) return "s";
        if (nearL && inY) return "w";
        if (nearR && inY) return "e";
        if (pos.X >= l && pos.X <= r && pos.Y >= t && pos.Y <= b) return "move";
        return "";
    }

    private void LivePreviewImage_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(LivePreviewImage);

        if (_floatingPasteActive)
        {
            if (_floatingPasteDragging && TryPointToNorm(pos, out double fnx, out double fny)
                && TryPointToNorm(_floatingPasteDragStart, out double fsx, out double fsy))
            {
                _floatingPasteCenterCanvas = new Point(
                    Math.Clamp(_floatingPasteCenterAtDragStart.X + (fnx - fsx), 0, 1),
                    Math.Clamp(_floatingPasteCenterAtDragStart.Y + (fny - fsy), 0, 1));
                RefreshFloatingPasteOverlay();
            }
            LivePreviewImage.Cursor = Cursors.SizeAll;
            return;
        }

        if (IsCloneStampMode && !_isCropping && _eyedropperColorField is null)
        {
            if (TryPointToNorm(pos, out double hnx, out double hny))
            {
                _cloneHoverNorm = new Point(hnx, hny);
                if (_clonePainting && _cloneSourceNorm is not null && !_clonePickSourceNext)
                    StampCloneAt(new Point(hnx, hny), deferPreview: true);
                long now = Environment.TickCount64;
                if (now - _lastCloneOverlayTick >= 33)
                {
                    _lastCloneOverlayTick = now;
                    RefreshCloneOverlay();
                }
            }
            LivePreviewImage.Cursor = Cursors.Cross;
            return;
        }

        if (IsFiligramBrushMode && !_isCropping && _eyedropperColorField is null)
        {
            if (TryPointToNorm(pos, out double fnx, out double fny))
            {
                _filigramHoverNorm = new Point(fnx, fny);
                long now = Environment.TickCount64;
                if (now - _lastCloneOverlayTick >= 33)
                {
                    _lastCloneOverlayTick = now;
                    RefreshCloneOverlay();
                }
            }
            LivePreviewImage.Cursor = Cursors.Cross;
            return;
        }

        if (!_isCropping)
        {
            if (IsFreeMarqueeMode)
            {
                string hit = HitTestPendingHandle(pos);
                LivePreviewImage.Cursor = hit switch
                {
                    "nw" or "se" => Cursors.SizeNWSE,
                    "ne" or "sw" => Cursors.SizeNESW,
                    "n" or "s" => Cursors.SizeNS,
                    "e" or "w" => Cursors.SizeWE,
                    "move" => Cursors.SizeAll,
                    _ => Cursors.Cross
                };
            }
            else if (IsPinSelectMode && _eyedropperColorField is null && _pendingCropRect is not null
                     && (IsPinShapedSelectionMode || _selectionPins.Count >= 2))
            {
                string hit = HitTestPendingHandle(pos);
                LivePreviewImage.Cursor = hit switch
                {
                    "nw" or "se" => Cursors.SizeNWSE,
                    "ne" or "sw" => Cursors.SizeNESW,
                    "n" or "s" => Cursors.SizeNS,
                    "e" or "w" => Cursors.SizeWE,
                    "move" => Cursors.SizeAll,
                    _ => Cursors.Cross
                };
            }
            else if (IsPinSelectMode && _eyedropperColorField is null)
            {
                LivePreviewImage.Cursor = Cursors.Cross;
            }
            return;
        }

        if (_cropDragMode == "create")
        {
            if (TryComputeNormRectFromPoints(_cropDragStart, pos, out var rect))
            {
                _pendingCropRect = rect;
                RefreshPendingCropOverlay();
                SyncCropPxBoxesFromPending();
            }
            return;
        }

        if (_cropRectAtDragStart is null || !TryPointToNorm(pos, out var nx, out var ny) || !TryPointToNorm(_cropDragStart, out var sx, out var sy))
            return;

        double dx = nx - sx;
        double dy = ny - sy;
        var o = _cropRectAtDragStart;

        if (_cropDragMode == "move")
        {
            _pendingCropRect = ClampNormRect(o.Left + dx, o.Top + dy, o.Width, o.Height);
            if (_pinDragging || ShouldDrivePinSelection)
                SyncPinsFromPendingRect(dx, dy);
        }
        else if (_cropDragMode == "resize")
        {
            double left = o.Left, top = o.Top, right = o.Left + o.Width, bottom = o.Top + o.Height;
            switch (_cropResizeHandle)
            {
                case "nw": left = o.Left + dx; top = o.Top + dy; break;
                case "ne": right = o.Left + o.Width + dx; top = o.Top + dy; break;
                case "sw": left = o.Left + dx; bottom = o.Top + o.Height + dy; break;
                case "se": right = o.Left + o.Width + dx; bottom = o.Top + o.Height + dy; break;
                case "n": top = o.Top + dy; break;
                case "s": bottom = o.Top + o.Height + dy; break;
                case "w": left = o.Left + dx; break;
                case "e": right = o.Left + o.Width + dx; break;
            }
            double l = Math.Min(left, right);
            double t = Math.Min(top, bottom);
            double w = Math.Abs(right - left);
            double h = Math.Abs(bottom - top);
            _pendingCropRect = ClampNormRect(l, t, w, h);
            if (_pinDragging || ShouldDrivePinSelection)
                SyncPinsFromPendingRect();
        }

        RefreshPendingCropOverlay();
        SyncCropPxBoxesFromPending();
        UpdateCropUi();
    }

    private void LivePreviewImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_floatingPasteDragging)
        {
            _floatingPasteDragging = false;
            if (LivePreviewImage.IsMouseCaptured)
                LivePreviewImage.ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

        if (_clonePainting)
        {
            _clonePainting = false;
            if (LivePreviewImage.IsMouseCaptured)
                LivePreviewImage.ReleaseMouseCapture();
            ScheduleLivePreview();
            e.Handled = true;
            return;
        }

        if (!_isCropping)
            return;

        CancelCropDrag();
        if (_selectionPins.Count > 0)
        {
            ApplyPinsToPendingSelection();
            RefreshPinOverlay();
        }
        UpdateCropUi();
        RefreshPendingCropOverlay();
        e.Handled = true;
    }

    private void LivePreviewImage_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsCloneStampMode && _eyedropperColorField is null)
        {
            var rpos = e.GetPosition(LivePreviewImage);
            if (TryPointToNorm(rpos, out double rnx, out double rny))
                SetCloneSource(new Point(rnx, rny));
            e.Handled = true;
            return;
        }

        if (IsPinSelectMode)
        {
            if (_selectionPins.Count == 0)
            {
                // Pin yoksa sağ tık: bekleyen seçimi temizle
                if (_pendingCropRect is not null)
                {
                    ClearPendingWhiteSelection();
                    e.Handled = true;
                }
                return;
            }

            _selectionPins.RemoveAt(_selectionPins.Count - 1);
            if (_selectionPins.Count == 0)
            {
                RefreshPinOverlay();
                RefreshPinButtonsUi();
                UpdateCropUi();
                RefreshSelectionOverlaysAfterModeChange();
            }
            else
            {
                ApplyPinsToPendingSelection();
                RefreshPinOverlay();
                RefreshPinButtonsUi();
                UpdateCropUi();
            }
            e.Handled = true;
            return;
        }

        // Serbest beyaz seçim: sağ tık ile ekrandan temizle
        if (IsFreeMarqueeMode && (_pendingCropRect is not null || _isCropping))
        {
            ClearPendingWhiteSelection();
            e.Handled = true;
        }
    }

    private void LivePreviewImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_floatingPasteActive && _eyedropperColorField is null)
        {
            var fpos = e.GetPosition(LivePreviewImage);
            // Çift tık veya Ctrl+tık: bırak; tek tık sürükle başlangıcı
            if (e.ClickCount >= 2 || (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (TryPointToNorm(fpos, out double px, out double py))
                    _floatingPasteCenterCanvas = new Point(px, py);
                ConfirmFloatingPaste();
                e.Handled = true;
                return;
            }

            _floatingPasteDragStart = fpos;
            _floatingPasteCenterAtDragStart = _floatingPasteCenterCanvas;
            _floatingPasteDragging = true;
            LivePreviewImage.CaptureMouse();
            e.Handled = true;
            return;
        }

        if (IsCloneStampMode && _eyedropperColorField is null)
        {
            if (LivePreviewImage.Source is not BitmapSource)
            {
                MessageBox.Show("Önizleme görseli hazır değil.", "Klon damga",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            CancelEyedropper();
            CancelCropDrag();
            var cpos = e.GetPosition(LivePreviewImage);
            if (!TryPointToNorm(cpos, out double cnx, out double cny))
            {
                e.Handled = true;
                return;
            }
            bool pickSource = _clonePickSourceNext
                              || _cloneSourceNorm is null
                              || (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;
            if (pickSource)
            {
                SetCloneSource(new Point(cnx, cny));
            }
            else
            {
                _clonePainting = true;
                _cloneLastStampNorm = null;
                LivePreviewImage.CaptureMouse();
                StampCloneAt(new Point(cnx, cny), deferPreview: true);
            }
            e.Handled = true;
            return;
        }

        if (IsFiligramBrushMode && _eyedropperColorField is null)
        {
            if (LivePreviewImage.Source is not BitmapSource)
            {
                MessageBox.Show("Önizleme görseli hazır değil.", "Filigram fırça",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            CancelEyedropper();
            CancelCropDrag();
            var fpos = e.GetPosition(LivePreviewImage);
            if (!TryPointToNorm(fpos, out double fnx, out double fny))
            {
                e.Handled = true;
                return;
            }
            // Tık: şekil+boyut ile yerleştir ve hemen temizle
            PlaceFiligramBrushAt(new Point(fnx, fny), applyNow: true);
            e.Handled = true;
            return;
        }

        if (IsPinSelectMode && _eyedropperColorField is null)
        {
            if (LivePreviewImage.Source is not BitmapSource)
            {
                MessageBox.Show(
                    "Önizleme görseli hazır değil.",
                    "Pin seçim",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            CancelEyedropper();
            var pinPos = e.GetPosition(LivePreviewImage);
            bool shaped = GetSelectedCloneBrushShape() != TextureCloneBrushShape.Normal;
            bool shiftMove = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

            // Sürükleme: şekilli tek pin, veya 2+ pin + Shift, veya kenar tutamaçları (2+ pin)
            // Aksi halde sol tık her zaman yeni pin ekler (yakın pin koyabilmek için)
            if (_selectionPins.Count > 0 && _pendingCropRect is not null)
            {
                string hit = HitTestPendingHandle(pinPos);
                bool allowDrag = !string.IsNullOrEmpty(hit) && (
                    (shaped && _selectionPins.Count == 1)
                    || (_selectionPins.Count >= 2 && (shiftMove || hit is "nw" or "ne" or "sw" or "se" or "n" or "s" or "e" or "w")));

                if (allowDrag)
                {
                    CancelCropDrag();
                    _cropDragStart = pinPos;
                    _cropRectAtDragStart = _pendingCropRect;
                    _pinsAtCropDragStart = _selectionPins.Select(p => new Point(p.X, p.Y)).ToList();
                    _pinDragging = true;
                    _isCropping = true;
                    if (hit is "nw" or "ne" or "sw" or "se" or "n" or "s" or "e" or "w")
                    {
                        _cropDragMode = "resize";
                        _cropResizeHandle = hit;
                    }
                    else
                    {
                        _cropDragMode = "move";
                        _cropResizeHandle = "";
                    }
                    LivePreviewImage.CaptureMouse();
                    e.Handled = true;
                    return;
                }
            }

            CancelCropDrag();
            if (TryPointToNorm(pinPos, out double pnx, out double pny))
            {
                if (_selectionPins.Count >= MaxSelectionPins)
                    _selectionPins.RemoveAt(0);
                _selectionPins.Add(new Point(pnx, pny));
                ApplyPinsToPendingSelection();
                RefreshPinOverlay();
                RefreshPinButtonsUi();
                UpdateCropUi();
            }
            e.Handled = true;
            return;
        }

        if (IsFreeMarqueeMode)
        {
            if (LivePreviewImage.Source is not BitmapSource)
            {
                MessageBox.Show(
                    "Önizleme görseli hazır değil. Önce bir şablon ve (isteğe bağlı) kaynak klasör seçin.",
                    "Seçim",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            CancelEyedropper();
            var pos = e.GetPosition(LivePreviewImage);

            // N dışı şekil: pin olmadan tıkla → şekil seçimi; ardından Bırak / Döndür
            if (GetSelectedCloneBrushShape() != TextureCloneBrushShape.Normal
                && TryPointToNorm(pos, out double snx, out double sny))
            {
                PlaceShapedSelectionAt(new Point(snx, sny));
                e.Handled = true;
                return;
            }

            string hit = HitTestPendingHandle(pos);
            _cropDragStart = pos;
            _cropRectAtDragStart = _pendingCropRect;
            _isCropping = true;

            if (hit is "nw" or "ne" or "sw" or "se" or "n" or "s" or "e" or "w")
            {
                _cropDragMode = "resize";
                _cropResizeHandle = hit;
            }
            else if (hit == "move")
            {
                _cropDragMode = "move";
                _cropResizeHandle = "";
            }
            else
            {
                _cropDragMode = "create";
                _cropResizeHandle = "";
                _pendingCropRect = null;
                SetCropOverlay(null);
            }

            LivePreviewImage.CaptureMouse();
            e.Handled = true;
            return;
        }

        if (_eyedropperColorField is null)
            return;

        if (LivePreviewImage.Source is not BitmapSource bitmap)
            return;

        var pickPos = e.GetPosition(LivePreviewImage);
        if (!PreviewColorSampler.TryPick(bitmap, LivePreviewImage, pickPos, out var hex))
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
        if (e.Key == Key.Escape && _floatingPasteActive)
        {
            CancelFloatingPaste(clearCopy: false);
            if (CloneStatusHint is not null)
                CloneStatusHint.Text = "";
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && _isCropping)
        {
            CancelCropDrag();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && _eyedropperColorField is not null)
        {
            CancelEyedropper();
            e.Handled = true;
            return;
        }

        if (_floatingPasteActive)
        {
            if (e.Key == Key.Enter)
            {
                ConfirmFloatingPaste();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Left)
            {
                if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                    NudgeFloatingPasteRotation(-5);
                else
                    NudgeFloatingPastePosition(-1, 0);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Right)
            {
                if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                    NudgeFloatingPasteRotation(+5);
                else
                    NudgeFloatingPastePosition(1, 0);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Up)
            {
                NudgeFloatingPastePosition(0, -1);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Down)
            {
                NudgeFloatingPastePosition(0, 1);
                e.Handled = true;
                return;
            }
        }

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (e.Key is Key.D0 or Key.NumPad0)
            {
                ResetPreviewZoom();
                e.Handled = true;
                return;
            }

            if (e.Key is Key.OemPlus or Key.Add)
            {
                ZoomPreviewBy(PreviewZoomStep);
                e.Handled = true;
                return;
            }

            if (e.Key is Key.OemMinus or Key.Subtract)
            {
                ZoomPreviewBy(1.0 / PreviewZoomStep);
                e.Handled = true;
            }

            if (e.Key == Key.C && SelectionCopyButton?.IsEnabled == true)
            {
                CopyCurrentSelectionToClipboard();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.V)
            {
                if (_floatingPasteActive)
                    ConfirmFloatingPaste();
                else
                    StartFloatingPaste(fromCurrentSelection: true);
                e.Handled = true;
            }
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
        _livePreviewSourceFile = null;
        _lastPreviewSourceForCropReset = null;
        ClearAllPerFilePreviewEdits();
        PreviewSourceCache.Invalidate();
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
        int generation = Interlocked.Increment(ref _previewGeneration);
        _ = RefreshLivePreviewAsync(token, generation);
    }

    private async Task RefreshLivePreviewAsync(CancellationToken ct, int generation)
    {
        LivePreviewResult result;
        try
        {
            try
            {
                // Kaydırıcı spam'inde gereksiz render birikmesini azalt
                await Task.Delay(350, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            // Daha yeni bir istek geldiyse bu nesli çalıştırma
            if (generation != _previewGeneration)
                return;

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

            if (ct.IsCancellationRequested || generation != _previewGeneration)
                return;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            if (ct.IsCancellationRequested || generation != _previewGeneration)
                return;
            result = new LivePreviewResult(null, "", "Önizleme hatası", false, ex.Message);
        }

        await Dispatcher.InvokeAsync(() =>
        {
            if (generation != _previewGeneration)
                return;
            ApplyLivePreviewResult(result);
        }, System.Windows.Threading.DispatcherPriority.Normal);
    }

    private IProductTemplate ResolvePreviewTemplate()
    {
        if (TemplateCombo.SelectedItem is TemplateListItem item)
            return item.Template;

        return TemplateRegistry.GetById("sablon-yok")
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
            _previewPixelWidth = result.OutputWidth;
            _previewPixelHeight = result.OutputHeight;
            UpdatePreviewZoomHostSize();
            RefreshPreviewZoomUi();
            Dispatcher.BeginInvoke(() =>
            {
                RefreshPendingCropOverlay();
                RefreshPinOverlay();
                RefreshCloneOverlay();
                RefreshFloatingPasteOverlay();
            }, System.Windows.Threading.DispatcherPriority.Loaded);
            SyncCropPxBoxesFromPending();
        }
        else
        {
            LivePreviewImage.Source = null;
            LivePreviewImage.Visibility = Visibility.Collapsed;
            _previewPixelWidth = 0;
            _previewPixelHeight = 0;
            PreviewPlaceholderText.Visibility = Visibility.Visible;
            PreviewPlaceholderText.Text = result.ErrorMessage is not null
                ? "Önizleme oluşturulamadı"
                : "Ürün görselini buraya sürükleyin\nveya sağ panelden klasör seçin";
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
        string focusName = !string.IsNullOrEmpty(_livePreviewSourceFile)
            ? Path.GetFileName(_livePreviewSourceFile)
            : "";
        PreviewCaptionText.Text = string.IsNullOrEmpty(focusName)
            ? result.Caption
            : $"Önizleme: {focusName} · {result.Caption}";

        bool realPhoto = result.Caption.Contains("Gerçek fotoğraf", StringComparison.Ordinal);
        PreviewModeBadgeText.Text = realPhoto ? "CANLI" : "DEMO";
        PreviewModeBadge.Background = realPhoto
            ? UiColorHelper.ToSolidBrush("#1B7F6E")
            : UiColorHelper.ToSolidBrush("#1B2A4A");
    }

    private string? TryGetPreviewSourceImageFile(string? folder)
    {
        if (!string.IsNullOrEmpty(_livePreviewSourceFile) && File.Exists(_livePreviewSourceFile))
        {
            bool processSelectedOnly = ProcessSelectedOnlyCheck?.IsChecked == true;
            if (!processSelectedOnly
                || SourceFileList.SelectedItems.Count == 0
                || IsSourcePathSelected(_livePreviewSourceFile))
            {
                return _livePreviewSourceFile;
            }
        }

        var firstSelected = FirstSelectedSourcePath();
        if (firstSelected is not null)
            return firstSelected;

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
            ExtendTemplateEdges = baseJob.ExtendTemplateEdges,
            EdgePadSampleRect = baseJob.EdgePadSampleRect,
            JpegQuality = baseJob.JpegQuality,
            SaveAsPng = baseJob.SaveAsPng,
            FileNamePattern = baseJob.FileNamePattern,
            TextOverlay = baseJob.TextOverlay,
            SamplePreviewCount = baseJob.SamplePreviewCount,
            ProcessOnlySelectedFiles = false,
            CropRect = baseJob.CropRect,
            CropOnlySelectedFiles = baseJob.CropOnlySelectedFiles,
            CropSelectedFilePaths = baseJob.CropSelectedFilePaths,
            WatermarkCleanOps = baseJob.WatermarkCleanOps,
            TextureCloneOps = baseJob.TextureCloneOps,
            SelectionPasteOps = baseJob.SelectionPasteOps,
            WatermarkCleanOpsByFile = baseJob.WatermarkCleanOpsByFile,
            TextureCloneOpsByFile = baseJob.TextureCloneOpsByFile,
            SelectionPasteOpsByFile = baseJob.SelectionPasteOpsByFile,
            CropRectByFile = baseJob.CropRectByFile
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
        if (OutputFormatCombo is not null)
            OutputFormatCombo.IsEnabled = !busy;
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

internal sealed record FiligramStyleItem(string Name, RonekaiImageFramer.Models.WatermarkCleanStyle Style);

internal sealed record CloneBrushShapeItem(string Name, RonekaiImageFramer.Models.TextureCloneBrushShape Shape);
