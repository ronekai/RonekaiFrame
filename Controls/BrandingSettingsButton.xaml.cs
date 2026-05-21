using System.Windows;
using System.Windows.Controls;

namespace RonekaiImageFramer.Controls;

public partial class BrandingSettingsButton : Button
{
    public event EventHandler? SettingsRequested;

    public BrandingSettingsButton()
    {
        InitializeComponent();
    }

    private void OnClick(object sender, RoutedEventArgs e) =>
        SettingsRequested?.Invoke(this, EventArgs.Empty);
}
