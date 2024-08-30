using System.Text.Json.Serialization;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Types.Enums;

namespace Sunrise.Server.API.Serializable.Response;

public class UserResponse(User user)
{

    [JsonPropertyName("user_id")]
    public int Id { get; set; } = user.Id;

    [JsonPropertyName("username")]
    public string Username { get; set; } = user.Username;

    [JsonPropertyName("country_code")]
    public string Country { get; set; } = ((CountryCodes)user.Country).ToString();

    [JsonPropertyName("privilege")]
    public string Privilege { get; set; } = user.Privilege.ToString();

    [JsonPropertyName("register_date")]
    public DateTime RegisterDate { get; set; } = user.RegisterDate;

    [JsonPropertyName("restricted")]
    public bool IsRestricted { get; set; } = user.IsRestricted;

    [JsonPropertyName("silenced_until")]
    public DateTime? SilencedUntil { get; set; } = user.SilencedUntil > DateTime.UtcNow ? user.SilencedUntil : null!;

    [JsonPropertyName("friends")]
    public List<int> FriendsList { get; set; } = user.FriendsList;
}