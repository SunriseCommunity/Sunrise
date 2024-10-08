using System.Text.Json.Serialization;
using Sunrise.Server.Database.Models;

namespace Sunrise.Server.API.Serializable.Response;

public class UserMedalResponse(Medal data, UserMedals? medal)
{
    [JsonPropertyName("id")] public int Id => data.Id;

    [JsonPropertyName("name")] public string Name => data.Name;

    [JsonPropertyName("description")] public string Description => data.Description;

    [JsonPropertyName("unlocked_at")] public DateTime? UnlockedAt => medal?.UnlockedAt;
}