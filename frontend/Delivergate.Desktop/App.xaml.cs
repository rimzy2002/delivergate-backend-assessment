using System.IO;
using System.Text.Json;
using System.Windows;
using Delivergate.Desktop.Configuration;
using Delivergate.Desktop.Services;
using Delivergate.Desktop.ViewModels;
using Delivergate.Desktop.Views;

namespace Delivergate.Desktop;

public partial class App : Application
{
    private ApiClient? _apiClient;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppSettings settings;

        try
        {
            settings = AppSettings.Load();
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException
                or JsonException or InvalidDataException)
        {
            MessageBox.Show(
                "Could not load appsettings.json. Check that the file " +
                "is present and ApiBaseUrl contains a valid HTTP " +
                "or HTTPS server address.",
                "Delivergate - Configuration error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(1);
            return;
        }

        _apiClient = new ApiClient(settings.ApiBaseUrl);

        var viewModel = new MainWindowViewModel(
            _apiClient,
            settings.ApiBaseUrl);

        MainWindow = new MainWindow(viewModel);
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _apiClient?.Dispose();
        base.OnExit(e);
    }
}
