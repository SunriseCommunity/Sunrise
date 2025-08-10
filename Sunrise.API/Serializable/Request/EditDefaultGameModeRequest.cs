using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Sunrise.Shared.Enums.Beatmaps;

namespace Sunrise.API.Serializable.Request;

public class EditDefaultGameModeRequest
{
    [JsonPropertyName("default_gamemode")]
    [Required]
    public required GameMode DefaultGameMode { get; set; }
}