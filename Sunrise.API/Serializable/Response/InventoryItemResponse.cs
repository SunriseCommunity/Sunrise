using System.Text.Json.Serialization;
using Sunrise.Shared.Enums;

namespace Sunrise.API.Serializable.Response;

public class InventoryItemResponse(ItemType itemType, int quantity)
{
    [JsonPropertyName("quantity")]
    public int Quantity { get; set; } = quantity;

    [JsonPropertyName("item_type")]
    public ItemType ItemType { get; set; } = itemType;
}