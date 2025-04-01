using System.Text.Json.Serialization;
using Sunrise.API.Services;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions.Users;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Utils.Converters;

namespace Sunrise.API.Serializable.Response;

public class UserResponse
{

    [JsonConstructor]
    public UserResponse()
    {
    }

    public UserResponse(DatabaseService database, SessionRepository sessionRepository, User user)
    {
        var avatarRecord = database.DbContext.UserFiles.FirstOrDefault(x => x.OwnerId == user.Id && x.Type == FileType.Avatar);
        var bannerRecord = database.DbContext.UserFiles.FirstOrDefault(x => x.OwnerId == user.Id && x.Type == FileType.Banner);

        var session = sessionRepository.GetSession(userId: user.Id);

        Id = user.Id;
        Username = user.Username;
        Description = user.Description;
        Country = ((CountryCode)user.Country).ToString();
        RegisterDate = user.RegisterDate;
        UserStatus = session != null ? session.Attributes.Status.ToText() : "Offline";
        AvatarUrl = $"https://a.{Configuration.Domain}/avatar/{user.Id}{(avatarRecord != null ? $"?{new DateTimeOffset(avatarRecord.UpdatedAt).ToUnixTimeMilliseconds()}" : "")}";
        BannerUrl = $"https://a.{Configuration.Domain}/banner/{user.Id}{(bannerRecord != null ? $"?{new DateTimeOffset(bannerRecord.UpdatedAt).ToUnixTimeMilliseconds()}" : "")}";
        LastOnlineTime = session != null ? session.Attributes.LastPingRequest : user.LastOnlineTime;
        IsRestricted = user.IsRestricted();
        SilencedUntil = user.SilencedUntil > DateTime.UtcNow ? user.SilencedUntil : null!;
        Badges = UserService.GetUserBadges(user);
    }

    [JsonPropertyName("user_id")]
    public int Id { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("country_code")]
    public string Country { get; set; }

    [JsonPropertyName("register_date")]
    [JsonConverter(typeof(DateTimeWithTimezoneConverter))]
    public DateTime RegisterDate { get; set; }

    [JsonPropertyName("avatar_url")]
    public string AvatarUrl { get; set; }

    [JsonPropertyName("banner_url")]
    public string BannerUrl { get; set; }


    [JsonPropertyName("last_online_time")]
    [JsonConverter(typeof(DateTimeWithTimezoneConverter))]
    public DateTime LastOnlineTime { get; set; }

    [JsonPropertyName("restricted")]
    public bool IsRestricted { get; set; }

    [JsonPropertyName("silenced_until")]
    [JsonConverter(typeof(DateTimeWithTimezoneConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? SilencedUntil { get; set; }

    [JsonPropertyName("badges")]
    public List<string> Badges { get; set; }

    [JsonPropertyName("user_status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UserStatus { get; set; }
}