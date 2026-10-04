using System.Text.Json.Serialization;

namespace Sunrise.Shared.Objects.Serializable.Performances;

public class CalculatorVersionResponse
{
    [JsonPropertyName("rosu")]
    public string Rosu { get; set; } = string.Empty;
}
