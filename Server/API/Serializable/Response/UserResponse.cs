using System.Text.Json.Serialization;
using Sunrise.Server.API.Services;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Types.Enums;

namespace Sunrise.Server.API.Serializable.Response;

public class UserResponse(User user, string? status = null)
{
    [JsonPropertyName("user_id")]
    public int Id { get; set; } = user.Id;

    [JsonPropertyName("username")]
    public string Username { get; set; } = user.Username;

    [JsonPropertyName("country_code")]
    public string Country { get; set; } = ((CountryCodes)user.Country).ToString();

    [JsonPropertyName("register_date")]
    public DateTime RegisterDate { get; set; } = user.RegisterDate;

    [JsonPropertyName("last_online_time")]
    public DateTime LastOnlineTime { get; set; } = user.LastOnlineTime;

    [JsonPropertyName("restricted")]
    public bool IsRestricted { get; set; } = user.IsRestricted;

    [JsonPropertyName("silenced_until")]
    public DateTime? SilencedUntil { get; set; } = user.SilencedUntil > DateTime.UtcNow ? user.SilencedUntil : null!;

    [JsonPropertyName("friends")]
    public List<int> FriendsList { get; set; } = user.FriendsList;

    [JsonPropertyName("badges")]
    public List<string> Badges { get; set; } = UserService.GetUserBadges(user);

    [JsonPropertyName("user_status")]
    public string? UserStatus { get; set; } = status ?? null;
}