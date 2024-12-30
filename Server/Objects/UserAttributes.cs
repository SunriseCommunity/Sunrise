using HOPEless.Bancho.Objects;
using Sunrise.Server.Application;
using Sunrise.Server.Database;
using Sunrise.Server.Database.Models.User;
using Sunrise.Server.Extensions;
using Sunrise.Server.Objects.Serializable;
using Sunrise.Server.Types.Enums;
using GameMode = Sunrise.Server.Types.Enums.GameMode;

namespace Sunrise.Server.Objects;

public class UserAttributes
{
    public UserAttributes(User user, Location location, LoginRequest loginRequest, bool usesOsuClient = true)
    {
        Latitude = location.Latitude;
        Longitude = location.Longitude;
        Timezone = location.TimeOffset;
        Country = Enum.TryParse(location.Country, out CountryCodes country)
            ? (short)country != 0 ? (short)country : null
            : null;
        User = user;
        UsesOsuClient = usesOsuClient;

        OsuVersion = loginRequest.Version;
        UserHash = loginRequest.ClientHash;
    }

    private User User { get; }
    public int Timezone { get; }
    private float Longitude { get; }
    private float Latitude { get; }
    private short? Country { get; }
    public string? OsuVersion { get; }
    public string? UserHash { get; }
    public DateTime LastPingRequest { get; private set; } = DateTime.UtcNow;
    public BanchoUserStatus Status { get; set; } = new();
    public bool ShowUserLocation { get; set; } = true;
    public bool IgnoreNonFriendPm { get; set; }
    public string? AwayMessage { get; set; }
    public bool IsBot { get; set; }
    public bool UsesOsuClient { get; set; }

    public async Task<BanchoUserPresence> GetPlayerPresence()
    {
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var userRank = IsBot ? 0 : await database.UserService.Stats.GetUserRank(User.Id, GetCurrentGameMode());

        return new BanchoUserPresence
        {
            UserId = User.Id,
            Username = User.Username,
            Timezone = Timezone,
            Latitude = ShowUserLocation ? Latitude : 0,
            Longitude = ShowUserLocation ? Longitude : 0,
            CountryCode = byte.Parse((Country ?? User.Country).ToString()),
            Permissions = User.GetPrivilegeRank(), // FIXME: Chat color doesn't work
            Rank = (int)userRank,
            PlayMode = GetCurrentGameMode().ToVanillaGameMode(),
            UsesOsuClient = UsesOsuClient
        };
    }

    public async Task<BanchoUserData> GetPlayerData()
    {
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var userStats = IsBot ? new UserStats() : await database.UserService.Stats.GetUserStats(User.Id, GetCurrentGameMode());
        var userRank = IsBot ? 0 : await database.UserService.Stats.GetUserRank(User.Id, GetCurrentGameMode());

        return new BanchoUserData
        {
            UserId = User.Id,
            Status = Status,
            Rank = (int)userRank,
            Performance = (short)userStats.PerformancePoints,
            Accuracy = (float)(userStats.Accuracy / 100f),
            Playcount = userStats.PlayCount,
            RankedScore = userStats.RankedScore,
            TotalScore = userStats.TotalScore
        };
    }


    public void UpdateLastPing()
    {
        LastPingRequest = DateTime.UtcNow;
    }


    public GameMode GetCurrentGameMode()
    {
        return ((GameMode)Status.PlayMode).EnrichWithMods(Status.CurrentMods);
    }

    public BanchoUserStatus GetPlayerStatus()
    {
        return Status;
    }
}