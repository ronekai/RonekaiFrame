using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        SetColorFromHex(initialHex);
    }

    private void SetColorFromHex(string hex)
    {
        if (!UiColorHelper.TryParseHex(hex, out var normalized))
            normalized = "#F5F6F8";

        SelectedHex = normalized;
        var (r, g, b) = UiColorHelper.ParseRgb(normalized);

        _updating = true;
        RedSlider.ValueChanged -= RgbSlider_ValueChanged;
        GreenSlider.ValueChanged -= RgbSlider_ValueChanged;
        BlueSlider.ValueChanged -= RgbSlider_ValueChanged;

        RedSlider.Value = r;
        GreenSlider.Value = g;
        BlueSlider.Value = b;
        HexBox.Text = normalized.ToUpperInvariant();
        RgbBox.Text = UiColorHelper.ToRgbString(r, g, b);

        RedSlider.ValueChanged += RgbSlider_ValueChanged;
        GreenSlider.ValueChanged += RgbSlider_ValueChanged;
        BlueSlider.ValueChanged += RgbSlider_ValueChanged;
        _updating = false;

        PreviewBorder.Background = new SolidColorBrush(Color.FromRgb(r, g, b));
    }

    private void RgbSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updating) return;

        byte r = (byte)RedSlider.Value;
        byte g = (byte)GreenSlider.Value;
        byte b = (byte)BlueSlider.Value;
        SelectedHex = UiColorHelper.ToHex(r, g, b);

        _updating = true;
        HexBox.Text = SelectedHex.ToUpperInvariant();
        RgbBox.Text = UiColorHelper.ToRgbString(r, g, b);
        _updating = false;

        PreviewBorder.Background = new SolidColorBrush(Color.FromRgb(r, g, b));
    }

    private void ApplyHexInput()
    {
        if (_updating) return;
        if (!UiColorHelper.TryParseHex(HexBox.Text, out var hex))
            return;
        SetColorFromHex(hex);
    }

    private void ApplyRgbInput()
    {
        if (_updating) return;
        if (!UiColorHelper.TryParseRgbString(RgbBox.Text, out var hex))
            return;
        SetColorFromHex(hex);
    }

    private void HexBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            ApplyHexInput();
    }

    private void HexBox_LostFocus(object sender, RoutedEventArgs e) => ApplyHexInput();

    private void RgbBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            ApplyRgbInput();
    }

    private void RgbBox_LostFocus(object sender, RoutedEventArgs e) => ApplyRgbInput();

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        ApplyHexInput();
        ApplyRgbInput();
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
