using System.IO;
using System.Windows;
using System.Windows.Threading;
using RonekaiImageFramer.Services;

namespace RonekaiImageFramer;

public partial class App : System.Windows.Application
{
    private static readonly string CrashLogPath = Path.Combine(
        AppContext.BaseDirectory, "son-hata.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += (_, args) =>
            LogCrash("Arka plan görevi", args.Exception);

        try
        {
            base.OnStartup(e);

            HeaderBrandingStore.Load();
            BrandLogoCatalog.EnsureBundledLogos();

            var login = new LoginWindow();
            if (login.ShowDialog() != true)
            {
                Shutdown();
                return;
            }

            MainWindow main;
            try
            {
                main = new MainWindow();
            }
            catch (Exception ex)
            {
                LogCrash("Ana pencere yüklenemedi", ex);
                ShowFatalError("Ana pencere açılamadı", ex);
                Shutdown(1);
                return;
            }

            MainWindow = main;
            main.Show();
            main.Activate();
        }
        catch (Exception ex)
        {
            LogCrash("Başlatma hatası", ex);
            ShowFatalError("Başlatma hatası", ex);
            Shutdown(1);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogCrash("Uygulama hatası", e.Exception);
        ShowFatalError("Beklenmeyen hata", e.Exception);
        e.Handled = true;
        Shutdown(1);
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            LogCrash("Kritik hata", ex);
    }

    private static void LogCrash(string title, Exception ex)
    {
        try
        {
            File.WriteAllText(CrashLogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {title}\n\n{ex}\n");
        }
        catch
        {
            // log yazılamazsa sessizce devam et
        }
    }

    private static void ShowFatalError(string title, Exception ex)
    {
        MessageBox.Show(
            $"{title}:\n\n{ex.Message}\n\nDetay dosyası:\n{CrashLogPath}",
            "PhonixFrame",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
