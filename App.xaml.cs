using System.Windows;
using DwgTimedEncryptor.Windows.Services;

namespace DwgTimedEncryptor.Windows;

public partial class App : Application
{
    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            StartupDiagnostics.Write(args.Exception);
            MessageBox.Show(
                $"ecrypt4Dwg 发生未处理错误。详细信息已写入：\n{StartupDiagnostics.LogPath}",
                "ecrypt4Dwg",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        try
        {
            var registry = new TaskRegistryService();
            var runner = new ScheduledCheckRunner(registry, new NetworkTimeService(), new FileCryptographyService());

            if (e.Args.Contains("--check", StringComparer.OrdinalIgnoreCase))
            {
                await runner.RunAsync();
                Shutdown();
                return;
            }

            new MainWindow(registry, runner).Show();
        }
        catch (Exception exception)
        {
            StartupDiagnostics.Write(exception);
            MessageBox.Show(
                $"ecrypt4Dwg 无法启动。详细信息已写入：\n{StartupDiagnostics.LogPath}",
                "ecrypt4Dwg",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }
}
