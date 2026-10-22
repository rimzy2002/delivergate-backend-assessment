using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Delivergate.Desktop.Services;

namespace Delivergate.Desktop.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly ApiClient _apiClient;
    private string _statusMessage = "Connection not checked yet.";
    private bool _isBusy;

    public MainWindowViewModel(
        ApiClient apiClient,
        string apiBaseUrl)
    {
        _apiClient = apiClient;
        ApiBaseUrl = apiBaseUrl;
        Auth = new AuthViewModel(apiClient);
        Workspace = new WorkspaceViewModel(apiClient.Session);
    }

    public string ApiBaseUrl { get; }
    public AuthViewModel Auth { get; }
    public WorkspaceViewModel Workspace { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value)
            {
                return;
            }

            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value)
            {
                return;
            }

            _isBusy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanCheckConnection));
        }
    }

    public bool CanCheckConnection => !IsBusy;

    public async Task CheckConnectionAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Checking connection...";

        try
        {
            await _apiClient.CheckHealthAsync();
            StatusMessage = "Connected. Backend health status is OK.";
        }
        catch (TaskCanceledException)
        {
            StatusMessage =
                "The request timed out. Check the backend and try again.";
        }
        catch (HttpRequestException)
        {
            StatusMessage =
                "Cannot reach the backend. Start the Go server " +
                "and check the API address.";
        }
        catch (JsonException)
        {
            StatusMessage =
                "The server returned invalid JSON. Check the /health endpoint.";
        }
        catch (NotSupportedException)
        {
            StatusMessage =
                "The server returned an unsupported response format.";
        }
        catch (InvalidDataException ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }
}
