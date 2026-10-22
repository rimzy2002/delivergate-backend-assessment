namespace Delivergate.Desktop.Models;

public sealed class AuthUser
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Email { get; init; } = "";
    public string Role { get; init; } = "";
}

public sealed class LoginResponse
{
    public string Token { get; init; } = "";
    public AuthUser? User { get; init; }
}

public sealed class CurrentUserResponse
{
    public AuthUser? User { get; init; }
}
