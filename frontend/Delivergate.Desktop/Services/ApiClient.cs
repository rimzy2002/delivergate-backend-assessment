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
        int revision = Session.Revision;
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/auth/me");
        // A per-request header avoids attaching a token to login or health requests.
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized && Session.Revision == revision) Session.Clear();
            throw AuthError(response.StatusCode, isLogin: false);
        }

        CurrentUserResponse? result = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        if (!ValidUser(result?.User))
        {
            throw new InvalidDataException("The server returned incomplete user details.");
        }
        return result!.User!;
    }

    public async Task<IReadOnlyList<InventoryItem>> GetItemsAsync(CancellationToken cancellationToken = default)
    {
        ItemsResponse result = await GetProtectedAsync<ItemsResponse>("api/items", cancellationToken);
        if (result.Items is null || result.Items.Any(item => item is null || item.Id <= 0 ||
            string.IsNullOrWhiteSpace(item.Name) || item.CategoryId <= 0 || item.Price <= 0 || item.Quantity < 0))
            throw new InvalidDataException("The server returned invalid inventory data.");
        return result.Items;
    }

    public async Task<IReadOnlyList<InventoryCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        CategoriesResponse result = await GetProtectedAsync<CategoriesResponse>("api/categories", cancellationToken);
        if (result.Categories is null || result.Categories.Any(category => category is null ||
            category.Id <= 0 || string.IsNullOrWhiteSpace(category.Name)))
            throw new InvalidDataException("The server returned invalid category data.");
        return result.Categories;
    }

    private async Task<T> GetProtectedAsync<T>(string path, CancellationToken cancellationToken)
    {
        string? token = Session.Token;
        int revision = Session.Revision;
        if (token is null) throw new ApiException(HttpStatusCode.Unauthorized, "Please sign in first.");
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // A delayed response from an old account must not sign out a newer one.
            if (response.StatusCode == HttpStatusCode.Unauthorized && Session.Revision == revision)
                Session.Clear();
            throw AuthError(response.StatusCode, isLogin: false);
        }
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
            ?? throw new InvalidDataException("The server returned an empty response.");
    }

    public async Task<InventoryItem> SaveItemAsync(int? id, ItemInput input, CancellationToken cancellationToken = default)
    {
        if (id is <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        using HttpResponseMessage response = await SendMutationAsync(
            id is null ? HttpMethod.Post : HttpMethod.Patch,
            id is null ? "api/items" : $"api/items/{id}", JsonContent.Create(input), "item", cancellationToken);
        ItemResponse? result = await response.Content.ReadFromJsonAsync<ItemResponse>(cancellationToken);
        InventoryItem? item = result?.Item;
        if (item is null || item.Id <= 0 || (id.HasValue && item.Id != id.Value) ||
            string.IsNullOrWhiteSpace(item.Name) || item.CategoryId <= 0 || item.Price <= 0 || item.Quantity < 0)
            throw new InvalidDataException("The server did not return a valid saved item. Refresh before retrying.");
        return item;
    }

    public async Task DeleteItemAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        using HttpResponseMessage response = await SendMutationAsync(
            HttpMethod.Delete, $"api/items/{id}", null, "item", cancellationToken);
        // The backend returns 204 No Content. Do not try to parse an empty JSON body.
        if (response.StatusCode != HttpStatusCode.NoContent)
            throw new InvalidDataException("Unexpected delete response. Refresh to check the result.");
    }

    private async Task<HttpResponseMessage> SendMutationAsync(
        HttpMethod method, string path, HttpContent? content, string resource, CancellationToken cancellationToken)
    {
        string? token = Session.Token;
        int revision = Session.Revision;
        if (token is null)
        {
            content?.Dispose();
            throw new ApiException(HttpStatusCode.Unauthorized, "Please sign in first.");
        }
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = content;
        HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode) return response;
        HttpStatusCode status = response.StatusCode;
        response.Dispose();
        if (status == HttpStatusCode.Unauthorized && Session.Revision == revision) Session.Clear();
        throw status switch
        {
            HttpStatusCode.BadRequest => new ApiException(status, $"The {resource} was rejected. Check the fields, then try again."),
            HttpStatusCode.NotFound => new ApiException(status, $"This {resource} no longer exists. Cancel the form and refresh the list."),
            HttpStatusCode.Conflict => new ApiException(status, resource == "customer"
                ? "A customer with this email already exists. Use a different email."
                : "The item conflicts with current server data. Refresh inventory."),
            _ => AuthError(status, isLogin: false)
        };
    }

    public async Task<IReadOnlyList<Customer>> GetCustomersAsync(CancellationToken cancellationToken = default)
    {
        CustomersResponse response = await GetProtectedAsync<CustomersResponse>("api/customers", cancellationToken);
        if (response.Customers is null || response.Customers.Any(customer => !ValidCustomer(customer)))
            throw new InvalidDataException("The server returned invalid customer data.");
        return response.Customers;
    }

    public async Task<Customer> SaveCustomerAsync(int? id, CustomerInput input, CancellationToken cancellationToken = default)
    {
        if (id is <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        using HttpResponseMessage response = await SendMutationAsync(id is null ? HttpMethod.Post : HttpMethod.Patch,
            id is null ? "api/customers" : $"api/customers/{id}", JsonContent.Create(input), "customer", cancellationToken);
        CustomerResponse? result = await response.Content.ReadFromJsonAsync<CustomerResponse>(cancellationToken);
        if (!ValidCustomer(result?.Customer) || (id.HasValue && result!.Customer!.Id != id.Value))
            throw new InvalidDataException("The server did not return a valid saved customer. Refresh before retrying.");
        return result!.Customer!;
    }

    public async Task DeleteCustomerAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        using HttpResponseMessage response = await SendMutationAsync(HttpMethod.Delete, $"api/customers/{id}", null, "customer", cancellationToken);
        if (response.StatusCode != HttpStatusCode.NoContent)
            throw new InvalidDataException("Unexpected delete response. Refresh to check the result.");
    }

    private static bool ValidCustomer(Customer? customer) => customer is not null && customer.Id > 0 &&
        !string.IsNullOrWhiteSpace(customer.Name) && !string.IsNullOrWhiteSpace(customer.Email) && !string.IsNullOrWhiteSpace(customer.Phone);

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
