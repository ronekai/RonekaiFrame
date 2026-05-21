using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RonekaiImageFramer.Ui;

namespace RonekaiImageFramer;

public partial class ColorPickerWindow : Window
{
    private bool _updating;

    public string SelectedHex { get; private set; } = "#FFFFFF";

    public ColorPickerWindow(string initialHex, Window? owner = null)
    {
        InitializeComponent();
        if (owner != null)
            Owner = owner;

        var (r, g, b) = UiColorHelper.ParseRgb(initialHex);
        _updating = true;
        RedSlider.ValueChanged -= RgbSlider_ValueChanged;
        GreenSlider.ValueChanged -= RgbSlider_ValueChanged;
        BlueSlider.ValueChanged -= RgbSlider_ValueChanged;
        RedSlider.Value = r;
        GreenSlider.Value = g;
        BlueSlider.Value = b;
        RedSlider.ValueChanged += RgbSlider_ValueChanged;
        GreenSlider.ValueChanged += RgbSlider_ValueChanged;
        BlueSlider.ValueChanged += RgbSlider_ValueChanged;
        _updating = false;
        UpdatePreview();
    }

    private void RgbSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updating) return;
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        byte r = (byte)RedSlider.Value;
        byte g = (byte)GreenSlider.Value;
        byte b = (byte)BlueSlider.Value;
        SelectedHex = UiColorHelper.ToHex(r, g, b);
        PreviewBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
        HexLabel.Text = SelectedHex.ToUpperInvariant();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
