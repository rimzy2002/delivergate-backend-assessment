using System.Text.Json.Serialization;

namespace Delivergate.Desktop.Models;

public sealed class HealthResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";
}
