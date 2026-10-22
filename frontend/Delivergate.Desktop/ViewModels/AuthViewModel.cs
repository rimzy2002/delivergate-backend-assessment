using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Net.Mail;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Delivergate.Desktop.Services;

namespace Delivergate.Desktop.ViewModels;

public sealed class AuthViewModel : INotifyPropertyChanged
{
    private readonly ApiClient _api;
    private string _email = "";
    private string _message = "Sign in with your shop account.";
    private bool _isBusy;
    private bool _wasSignedIn;

    public AuthViewModel(ApiClient api)
    {
        _api = api;
        _wasSignedIn = api.Session.IsSignedIn;
        _api.Session.Changed += SessionChanged;
    }

    public string Email { get => _email; set => Set(ref _email, value); }
    public string Message { get => _message; private set => Set(ref _message, value); }
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            Set(ref _isBusy, value);
            Notify(nameof(CanInteract));
        }
    }
    public bool CanInteract => !IsBusy;
    public bool IsSignedIn => _api.Session.IsSignedIn;
    public bool IsSignedOut => !IsSignedIn;
    public string UserName => _api.Session.User?.Name ?? "";
    public string UserEmail => _api.Session.User?.Email ?? "";
    public string UserRole => _api.Session.User?.Role ?? "";

    public async Task LoginAsync(string password)
    {
        if (IsBusy || IsSignedIn) return;
        string email = Email.Trim();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            Message = "Enter your email and password.";
            return;
        }
        if (!MailAddress.TryCreate(email, out var address) || address.Address != email)
        {
            Message = "Enter a valid email address.";
            return;
        }
        Email = email;
        await RunAsync(() => _api.LoginAsync(email, password), "Signing in...", "You are signed in.");
    }

    public async Task VerifySessionAsync()
    {
        if (IsBusy || !IsSignedIn) return;
        await RunAsync(_api.RefreshSessionAsync, "Checking session...", "Your session is valid.");
    }

    public void Logout()
    {
        if (IsBusy) return;
        _api.Session.Clear();
        Message = "You have signed out.";
    }

    private async Task RunAsync(Func<Task> action, string pending, string success)
    {
        IsBusy = true;
        Message = pending;
        try
        {
            await action();
            Message = success;
        }
        catch (ApiException ex) { Message = ex.Message; }
        catch (HttpRequestException) { Message = "Cannot reach the backend. Check the server and try again."; }
        catch (OperationCanceledException) { Message = "The request timed out. Please try again."; }
        catch (JsonException) { Message = "The server returned invalid JSON. Please contact the administrator."; }
        catch (InvalidDataException ex) { Message = ex.Message; }
        catch (NotSupportedException) { Message = "The server returned an unsupported response format."; }
        finally { IsBusy = false; }
    }

    private void SessionChanged()
    {
        if (_wasSignedIn && !IsSignedIn) Message = "Your session ended. Please sign in again.";
        _wasSignedIn = IsSignedIn;
        Notify(nameof(IsSignedIn));
        Notify(nameof(IsSignedOut));
        Notify(nameof(UserName));
        Notify(nameof(UserEmail));
        Notify(nameof(UserRole));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        Notify(name);
    }
}
