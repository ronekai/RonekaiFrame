using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
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
    private string? _customLogoPath;

    public MainWindow()
    {
        InitializeComponent();
        LogoOpacitySlider.ValueChanged += LogoOpacitySlider_ValueChanged;
        LogList.ItemsSource = _log;

        InitializeImageBrandFields();

        TemplateCombo.ItemsSource = TemplateRegistry.Templates
            .Select(t => new TemplateListItem(t))
            .ToList();
        TemplateCombo.SelectedIndex = 0;

        RefreshExportResolutionComboItems();
        RefreshExportResolutionHint();

        ColorPackCombo.ItemsSource = ColorPackRegistry.All;
        ColorPackCombo.SelectedIndex = 0;
        RefreshColorPackUi();

        LogoModeCombo.ItemsSource = LogoModeRegistry.All;
        LogoModeCombo.SelectedIndex = 0;
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
        ScheduleLivePreview();
    }

    private void InitializeImageBrandFields()
    {
        var brand = ImageBrandStore.Load();
        _updatingBrandFields = true;
        ImageBrandMainBox.Text = brand.MainText;
        ImageBrandSuffixBox.Text = brand.SuffixText;
        _updatingBrandFields = false;
    }

    private ImageBrandSettings BuildImageBrandSettings()
    {
        return new ImageBrandSettings
        {
            MainText = string.IsNullOrWhiteSpace(ImageBrandMainBox.Text)
                ? "RONEKAI"
                : ImageBrandMainBox.Text.Trim(),
            SuffixText = ImageBrandSuffixBox.Text?.Trim() ?? ""
        };
    }

    private void PersistImageBrandSettings()
    {
        if (_updatingBrandFields)
            return;
        ImageBrandStore.Save(BuildImageBrandSettings());
    }

    private void ImageBrand_TextChanged(object sender, TextChangedEventArgs e)
    {
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
            LogoFormatLabel.Text = loaded.Kind == LogoFileKind.Png
                ? $"Format: PNG (şeffaflık korunur) — {Path.GetFileName(effective)}"
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
            TemplateDescription.Text = $"{item.Description}\nBoyut: {item.SizeLabel}";
        RefreshExportResolutionComboItems();
        ScheduleLivePreview();
    }

    private void RefreshExportResolutionComboItems()
    {
        int? tw = null;
        int? th = null;
        if (TemplateCombo.SelectedItem is TemplateListItem templateItem)
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
        RefreshColorPackUi();
        ScheduleLivePreview();
    }

    private void RefreshColorPackUi()
    {
        if (ColorPackCombo.SelectedItem is not ColorPackListItem item)
            return;

        ColorPackDescription.Text = item.Theme.IsCustom
            ? item.Description
            : $"{item.Description}\n\nZemin / RONEKAI / .DEN için Seç… ile rengi değiştirebilirsiniz.";

        if (item.Theme.IsCustom)
            ApplyColorPreviews(_customBackgroundHex, _customRonekaiHex, _customDenHex);
        else
            ApplyColorPreviews(item.Theme.BackgroundHex, item.Theme.RonekaiHex, item.Theme.DenHex);
    }

    /// <summary>Hazır paletten özel moda geçer; mevcut palet renklerini özel alanlara kopyalar.</summary>
    private void ActivateCustomColorsFromSelection()
    {
        if (ColorPackCombo.SelectedItem is ColorPackListItem item && !item.Theme.IsCustom)
        {
            _customBackgroundHex = item.Theme.BackgroundHex;
            _customRonekaiHex = item.Theme.RonekaiHex;
            _customDenHex = item.Theme.DenHex;
        }

        SelectCustomColorPack();
    }

    private void ApplyColorPreviews(string bg, string ronekai, string den)
    {
        BackgroundColorPreview.Background = UiColorHelper.ToSolidBrush(bg);
        RonekaiColorPreview.Background = UiColorHelper.ToSolidBrush(ronekai);
        DenColorPreview.Background = UiColorHelper.ToSolidBrush(den);
        BackgroundColorHex.Text = bg.ToUpperInvariant();
        RonekaiColorHex.Text = ronekai.ToUpperInvariant();
        DenColorHex.Text = den.ToUpperInvariant();
    }

    private BrandColorTheme BuildColorTheme()
    {
        if (ColorPackCombo.SelectedItem is ColorPackListItem item && !item.Theme.IsCustom)
            return item.Theme;

        return BrandColorTheme.CreateCustom(_customBackgroundHex, _customRonekaiHex, _customDenHex);
    }

    private void PickBackgroundColor_Click(object sender, RoutedEventArgs e) =>
        PickCustomColor(ref _customBackgroundHex);

    private void PickRonekaiColor_Click(object sender, RoutedEventArgs e) =>
        PickCustomColor(ref _customRonekaiHex);

    private void PickDenColor_Click(object sender, RoutedEventArgs e) =>
        PickCustomColor(ref _customDenHex);

    private void PickCustomColor(ref string targetHex)
    {
        ActivateCustomColorsFromSelection();

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

    private void SelectCustomColorPack()
    {
        var custom = ColorPackRegistry.GetCustomItem();
        if (custom is null) return;
        ColorPackCombo.SelectedItem = custom;
    }

    private void LogoModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshLogoModeUi();
        ScheduleLivePreview();
    }

    private void RefreshLogoModeUi()
    {
        if (LogoModeCombo.SelectedItem is not LogoModeListItem item)
            return;

        LogoModeDescription.Text = item.Description;
        bool usesLogo = item.Mode != LogoOverlayMode.None;
        LogoOpacitySlider.IsEnabled = usesLogo;
        RefreshLogoPathUi();

        if (usesLogo)
        {
            _updatingOpacity = true;
            LogoOpacitySlider.Value = LogoOverlaySettings.DefaultOpacity(item.Mode) * 100;
            _updatingOpacity = false;
            UpdateOpacityLabel();
        }
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
        RefreshImageCount();
        UpdateOutputPreview();
        ScheduleLivePreview();
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
            Filter = "Logo dosyaları|*.png;*.jpg;*.jpeg;*.heic;*.heif;*.webp;*.bmp|PNG|*.png|JPEG|*.jpg;*.jpeg|Mac HEIC|*.heic;*.heif|Tüm dosyalar|*.*",
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
        try
        {
            await Task.Delay(180, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (TemplateCombo.SelectedItem is not TemplateListItem templateItem)
            return;

        var theme = BuildColorTheme();
        var logo = BuildLogoSettings();
        var imageBrand = BuildImageBrandSettings();
        var exportProfile = GetSelectedExportProfile();
        int? sampleW = null;
        int? sampleH = null;
        TryGetFirstSourceImageSize(SourceFolderBox.Text?.Trim(), out sampleW, out sampleH);

        var result = await Task.Run(
            () => TemplatePreviewService.Render(
                templateItem.Template, theme, logo, imageBrand, exportProfile, sampleW, sampleH),
            ct);

        if (ct.IsCancellationRequested)
            return;

        await Dispatcher.InvokeAsync(() => ApplyLivePreviewResult(result), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void ApplyLivePreviewResult(LivePreviewResult result)
    {
        if (result.Success && result.Image is not null)
        {
            LivePreviewImage.Source = result.Image;
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
        var path = SourceFolderBox.Text?.Trim();
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
                    ? $"{count} resim (alt klasörler dahil, {heif} HEIC)"
                    : $"{count} resim (alt klasörler dahil)"
                : "Resim yok — .heic/.jpg dosyalarını bu klasöre veya alt klasöre koyun";
        }
        catch
        {
            ImageCountText.Text = "";
        }
    }

    private void UpdateOutputPreview()
    {
        var source = SourceFolderBox.Text?.Trim();
        OutputPathText.Text = AppPaths.PreviewOutputPath(source);
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
            LogoFilePath = ResolveActiveLogoPath()
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

        var source = SourceFolderBox.Text?.Trim();
        if (string.IsNullOrEmpty(source) || !Directory.Exists(source))
        {
            MessageBox.Show("Geçerli bir kaynak klasör seçin.", "PhonixFrame",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var images = BatchProcessor.FindImages(source!);
        if (images.Count == 0)
        {
            MessageBox.Show(
                $"Klasörde desteklenen görsel yok.\n\n{ImageInputCatalog.SupportedFormatsDescription}",
                "PhonixFrame",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var colorTheme = BuildColorTheme();
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
        string outputFolder = AppPaths.CreateOutputFolder(source!);
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
            var result = await BatchProcessor.ProcessFolderAsync(
                source,
                selected.Template,
                outputFolder,
                colorTheme,
                logoSettings,
                imageBrand,
                exportProfile,
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
        CancelButton.IsEnabled = busy;
        TemplateCombo.IsEnabled = !busy;
        ColorPackCombo.IsEnabled = !busy;
        PickBackgroundColorButton.IsEnabled = !busy;
        PickRonekaiColorButton.IsEnabled = !busy;
        PickDenColorButton.IsEnabled = !busy;
        LogoModeCombo.IsEnabled = !busy;
        UseDefaultLogoCheck.IsEnabled = !busy;
        SourceFolderBox.IsEnabled = !busy;
        ExportResolutionCombo.IsEnabled = !busy;
        ImageBrandMainBox.IsEnabled = !busy;
        ImageBrandSuffixBox.IsEnabled = !busy;
        ProcessButton.Content = busy ? "İşleniyor…" : "Resimleri İşle";

        if (!busy)
            RefreshLogoModeUi();
    }
}
