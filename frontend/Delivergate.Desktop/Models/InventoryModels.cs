using System.Text.Json.Serialization;

namespace Delivergate.Desktop.Models;

public sealed class InventoryCategory
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
}

public sealed class InventoryItem
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    [JsonPropertyName("category_id")]
    public int CategoryId { get; init; }
    public InventoryCategory? Category { get; init; }
    public decimal Price { get; init; }
    public int Quantity { get; init; }
    public string CategoryName => Category?.Name ?? "Unknown category";
}

public sealed class ItemsResponse
{
    public List<InventoryItem>? Items { get; init; }
}

public sealed class CategoriesResponse
{
    public List<InventoryCategory>? Categories { get; init; }
}

public sealed record ItemInput(string Name, string Description,
    [property: JsonPropertyName("category_id")] int CategoryId, decimal Price, int Quantity);

public sealed class ItemResponse
{
    public InventoryItem? Item { get; init; }
}
