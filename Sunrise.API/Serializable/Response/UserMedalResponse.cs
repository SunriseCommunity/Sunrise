using System.Text.Json.Serialization;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Utils.Converters;

namespace Sunrise.API.Serializable.Response;

public class UserMedalResponse(Medal data, UserMedals? medal)
{
    [JsonPropertyName("id")]
    public int Id => data.Id;

    [JsonPropertyName("name")]
    public string Name => data.Name;

    [JsonPropertyName("description")]
    public string Description => data.Description;

    [JsonPropertyName("unlocked_at")]
    [JsonConverter(typeof(DateTimeWithTimezoneConverter))]
    public DateTime? UnlockedAt => medal?.UnlockedAt;
}