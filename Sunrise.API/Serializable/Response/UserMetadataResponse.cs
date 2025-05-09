using System.Text.Json.Serialization;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Helpers;

namespace Sunrise.API.Serializable.Response;

public class UserMetadataResponse
{
    [JsonConstructor]
    public UserMetadataResponse() { }

    public UserMetadataResponse(UserMetadata metadata)
    {
        Playstyle = JsonStringFlagEnumHelper.SplitFlags(metadata.Playstyle);
        Location = metadata.Location;
        Interest = metadata.Interest;
        Occupation = metadata.Occupation;
        Telegram = metadata.Telegram;
        Twitch = metadata.Twitch;
        Twitter = metadata.Twitter;
        Discord = metadata.Discord;
        Website = metadata.Website;
    }

    [JsonPropertyName("playstyle")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public IEnumerable<UserPlaystyle> Playstyle { get; set; }

    [JsonPropertyName("location")]
    public string Location { get; set; }

    [JsonPropertyName("interest")]
    public string Interest { get; set; }

    [JsonPropertyName("occupation")]
    public string Occupation { get; set; }

    [JsonPropertyName("telegram")]
    public string Telegram { get; set; }

    [JsonPropertyName("twitch")]
    public string Twitch { get; set; }

    [JsonPropertyName("twitter")]
    public string Twitter { get; set; }

    [JsonPropertyName("discord")]
    public string Discord { get; set; }

    [JsonPropertyName("website")]
    public string Website { get; set; }
}