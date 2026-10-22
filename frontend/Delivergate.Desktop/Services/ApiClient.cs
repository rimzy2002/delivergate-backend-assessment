using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Delivergate.Desktop.Models;

namespace Delivergate.Desktop.Services;

public sealed class ApiClient : IDisposable
{
    private readonly HttpClient _httpClient;

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

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
