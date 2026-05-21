using System.Windows;
using System.Windows.Input;
using RonekaiImageFramer.Services;

namespace RonekaiImageFramer;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        BrandingSettings.SettingsRequested += (_, _) => OpenBrandingSettings();
        Loaded += (_, _) =>
        {
            BrandingHeader.RefreshBranding();
            PasswordBox.Focus();
        };
    }

    private void OpenBrandingSettings()
    {
        var dialog = new HeaderBrandingWindow(HeaderBrandingStore.Current, this);
        if (dialog.ShowDialog() == true)
            BrandingHeader.RefreshBranding();
    }

    private void Login_Click(object sender, RoutedEventArgs e) => TryLogin();

    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            TryLogin();
    }

    private void TryLogin()
    {
        if (!AppAuth.Verify(PasswordBox.Password))
        {
            MessageBox.Show("Hatalı şifre.", "Giriş", MessageBoxButton.OK, MessageBoxImage.Warning);
            PasswordBox.Clear();
            PasswordBox.Focus();
            return;
        }

        DialogResult = true;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DialogResult != true)
            DialogResult = false;
        base.OnClosed(e);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
