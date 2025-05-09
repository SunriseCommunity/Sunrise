using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Sunrise.Shared.Enums.Users;

namespace Sunrise.API.Serializable.Request;

public class EditUserMetadataRequest
{
    [JsonPropertyName("playstyle")]
    public IEnumerable<UserPlaystyle>? Playstyle { get; set; }

    [JsonPropertyName("location")]
    [MaxLength(32)]
    public string? Location { get; set; } = null;

    [JsonPropertyName("interest")]
    [MaxLength(32)]
    public string? Interest { get; set; } = null;

    [JsonPropertyName("occupation")]
    [MaxLength(32)]
    public string? Occupation { get; set; } = null;

    [JsonPropertyName("telegram")]
    [MaxLength(32)]
    public string? Telegram { get; set; } = null;

    [JsonPropertyName("twitch")]
    [MaxLength(32)]
    public string? Twitch { get; set; } = null;

    [JsonPropertyName("twitter")]
    [MaxLength(32)]
    public string? Twitter { get; set; } = null;

    [JsonPropertyName("discord")]
    [MaxLength(32)]
    public string? Discord { get; set; } = null;

    [JsonPropertyName("website")]
    [MaxLength(200)]
    public string? Website { get; set; } = null;
}