using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Delivergate.Desktop.Models;

namespace Delivergate.Desktop.Services;

public sealed class ApiClient : IDisposable
{
    private readonly HttpClient _httpClient;
    public UserSession Session { get; } = new();

    public ApiClient(string baseUrl) : this(new HttpClient(), baseUrl)
    {
    }

    // The client is owned here. Tests can supply a controlled HTTP transport.
    internal ApiClient(HttpClient httpClient, string baseUrl)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(10);

        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task CheckHealthAsync()
    {
        using HttpResponseMessage response =
            await _httpClient.GetAsync("health");

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidDataException(
                $"Server returned HTTP {(int)response.StatusCode}. " +
                "Check the API address and backend.");
        }

        HealthResponse? health =
            await response.Content.ReadFromJsonAsync<HealthResponse>();

        if (health is null ||
            !string.Equals(health.Status, "ok",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "The server responded, but its health status was not 'ok'.");
        }
    }

    public async Task LoginAsync(string email, string password)
    {
        Session.Clear();
        using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
            "api/auth/login", new { email = email.Trim(), password });

        if (!response.IsSuccessStatusCode)
        {
            throw AuthError(response.StatusCode, isLogin: true);
        }

        LoginResponse? login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        if (login is null || string.IsNullOrWhiteSpace(login.Token) ||
            login.Token.Any(char.IsWhiteSpace) || !ValidUser(login.User))
        {
            throw new InvalidDataException("The server returned an incomplete login response.");
        }

        // Do not consider the session authenticated until the server accepts the token.
        AuthUser user = await FetchCurrentUserAsync(login.Token);
        if (user.Id != login.User!.Id)
        {
            throw new InvalidDataException("The server returned inconsistent user details.");
        }
        Session.Set(login.Token, user);
    }

    public async Task RefreshSessionAsync()
    {
        string? token = Session.Token;
        if (token is null)
        {
            throw new ApiException(HttpStatusCode.Unauthorized, "Please sign in first.");
        }

        AuthUser user = await FetchCurrentUserAsync(token);
        // Do not restore a session that was cleared while the request was pending.
        if (Session.Token == token) Session.Set(token, user);
    }

    private async Task<AuthUser> FetchCurrentUserAsync(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/auth/me");
        // A per-request header avoids attaching a token to login or health requests.
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized) Session.Clear();
            throw AuthError(response.StatusCode, isLogin: false);
        }

        CurrentUserResponse? result = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        if (!ValidUser(result?.User))
        {
            throw new InvalidDataException("The server returned incomplete user details.");
        }
        return result!.User!;
    }

    private static bool ValidUser(AuthUser? user) =>
        user is not null && user.Id > 0 &&
        !string.IsNullOrWhiteSpace(user.Name) && !string.IsNullOrWhiteSpace(user.Email) &&
        user.Role is "owner" or "manager" or "cashier";

    private static ApiException AuthError(HttpStatusCode status, bool isLogin) => new(status,
        status switch
        {
            HttpStatusCode.Unauthorized => isLogin
                ? "Invalid email or password. Please try again."
                : "Your session has expired or is no longer valid. Please sign in again.",
            HttpStatusCode.Forbidden => "You do not have permission to perform this action.",
            HttpStatusCode.BadRequest => "Check your email and password and try again.",
            HttpStatusCode.TooManyRequests => "Too many requests. Please wait and try again.",
            _ => $"The server could not complete the request (HTTP {(int)status}). Please try again."
        });

    public void Dispose()
    {
        Session.Clear();
        _httpClient.Dispose();
    }
}
