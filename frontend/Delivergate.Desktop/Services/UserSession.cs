using Delivergate.Desktop.Models;

namespace Delivergate.Desktop.Services;

// Memory only: restarting the application always requires signing in again.
public sealed class UserSession
{
    internal string? Token { get; private set; }
    public AuthUser? User { get; private set; }
    public bool IsSignedIn => Token is not null && User is not null;
    public event Action? Changed;

    internal void Set(string token, AuthUser user)
    {
        Token = token;
        User = user;
        Changed?.Invoke();
    }

    public void Clear()
    {
        Token = null;
        User = null;
        Changed?.Invoke();
    }
}
