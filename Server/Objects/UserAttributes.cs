using HOPEless.Bancho.Objects;
using osu.Shared;
using Sunrise.Server.Data;
using Sunrise.Server.Objects.Models;
using Sunrise.Server.Objects.Serializable;
using Sunrise.Server.Types.Enums;

namespace Sunrise.Server.Objects;

public class UserAttributes
{
    private readonly SunriseDb _database;

    public UserAttributes(User user, Location location, SunriseDb database, bool usesOsuClient = true)
    {
        _database = database;

        Latitude = location.Latitude;
        Longitude = location.Longitude;
        Timezone = location.TimeOffset;
        Country = Enum.TryParse(location.Country, out CountryCodes country) ? (short)country != 0 ? (short)country : null : null;
        User = user;
        UsesOsuClient = usesOsuClient;
    }

    private User User { get; }
    public int Timezone { get; }
    private float Longitude { get; }
    private float Latitude { get; }
    private short? Country { get; }
    public DateTime LastLogin { get; set; } = DateTime.UtcNow;
    public DateTime LastPingRequest { get; private set; } = DateTime.UtcNow;
    public BanchoUserStatus Status { get; set; } = new();
    public bool ShowUserLocation { get; set; } = true;
    public bool IgnoreNonFriendPm { get; set; }
    public string? AwayMessage { get; set; }
    public bool IsBot { get; set; }
    public bool UsesOsuClient { get; set; }

    public async Task<BanchoUserPresence> GetPlayerPresence()
    {
        var userRank = IsBot ? 0 : await _database.GetUserRank(User.Id, GetCurrentGameMode());

        return new BanchoUserPresence
        {
            UserId = User.Id,
            Username = User.Username,
            Timezone = Timezone,
            Latitude = ShowUserLocation ? Latitude : 0,
            Longitude = ShowUserLocation ? Longitude : 0,
            CountryCode = byte.Parse((Country ?? User.Country).ToString()),
            Permissions = User.Privilege,
            Rank = (int)userRank,
            PlayMode = GetCurrentGameMode(),
            UsesOsuClient = UsesOsuClient
        };
    }

    public async Task<BanchoUserData> GetPlayerData()
    {
        var userStats = IsBot ? new UserStats() : await _database.GetUserStats(User.Id, GetCurrentGameMode());
        var userRank = IsBot ? 0 : await _database.GetUserRank(User.Id, GetCurrentGameMode());

        return new BanchoUserData
        {
            UserId = User.Id,
            Status = Status,
            Rank = (int)userRank,
            Performance = userStats.PerformancePoints,
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
        return Status.PlayMode;
    }

    public BanchoUserStatus GetPlayerStatus()
    {
        return Status;
    }
}