using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using RonekaiImageFramer.Models;
using RonekaiImageFramer.Services;

namespace RonekaiImageFramer.Ui;

public static class HeaderBrandingApplier
{
    public static void Apply(
        HeaderBrandingSettings settings,
        Image logoImage,
        Panel titleRow,
        TextBlock mainText,
        TextBlock suffixText,
        TextBlock taglineText,
        bool showTagline = true)
    {
        mainText.Text = settings.MainText;
        suffixText.Text = settings.SuffixText;
        taglineText.Text = settings.Tagline;

        bool showTitle = settings.Mode != HeaderBrandingMode.Logo;
        bool showLogo = settings.Mode != HeaderBrandingMode.Text;

        titleRow.Visibility = showTitle ? Visibility.Visible : Visibility.Collapsed;
        suffixText.Visibility = showTitle && !string.IsNullOrWhiteSpace(settings.SuffixText)
            ? Visibility.Visible
            : Visibility.Collapsed;

        taglineText.Visibility = showTagline && !string.IsNullOrWhiteSpace(settings.Tagline)
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (!showLogo)
        {
            logoImage.Visibility = Visibility.Collapsed;
            logoImage.Source = null;
            return;
        }

        var path = ResolveLogoPath(settings.LogoPath);
        if (path is null)
        {
            logoImage.Visibility = Visibility.Collapsed;
            logoImage.Source = null;
            return;
        }

        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            logoImage.Source = bmp;
            logoImage.MaxHeight = 52;
            logoImage.Visibility = Visibility.Visible;
        }
        catch
        {
            logoImage.Visibility = Visibility.Collapsed;
            logoImage.Source = null;
        }
    }

    private static string? ResolveLogoPath(string? customPath)
    {
        if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
            return Path.GetFullPath(customPath);

        return LogoProvider.ResolveLogoPath(null);
    }
}
