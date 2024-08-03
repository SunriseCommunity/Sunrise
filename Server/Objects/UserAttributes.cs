using HOPEless.Bancho.Objects;
using osu.Shared;
using Sunrise.Server.Data;
using Sunrise.Server.Objects.Models;
using Sunrise.Server.Objects.Serializable;

namespace Sunrise.Server.Objects;

public class UserAttributes
{
    private readonly SunriseDb _database;

    public UserAttributes(User user, Location location, SunriseDb database)
    {
        _database = database;
        var coordinates = location.Loc.Split(',');

        Latitude = float.Parse(coordinates[0]);
        Longitude = float.Parse(coordinates[1]);
        Timezone = location.TimeOffset;
        User = user;
        LastLogin = DateTime.UtcNow;
        LastPingRequest = DateTime.UtcNow;
        Status = new BanchoUserStatus();
    }

    private int Timezone { get; }
    private float Longitude { get; }
    private float Latitude { get; }
    private User User { get; }
    public DateTime LastLogin { get; set; }
    public DateTime LastPingRequest { get; set; }
    public BanchoUserStatus Status { get; set; }

    public async Task<BanchoUserPresence> GetPlayerPresence()
    {
        var userRank = await _database.GetUserRank(User.Id, GetCurrentGameMode());

        return new BanchoUserPresence
        {
            UserId = User.Id,
            Username = User.Username,
            Timezone = Timezone,
            Latitude = Latitude,
            Longitude = Longitude,
            CountryCode = byte.Parse(User.Country.ToString()),
            Permissions = User.Privilege,
            Rank = (int)userRank,
            PlayMode = GetCurrentGameMode(),
            UsesOsuClient = true
        };
    }

    public async Task<BanchoUserData> GetPlayerData()
    {
        var userStats = await _database.GetUserStats(User.Id, GetCurrentGameMode());
        var userRank = await _database.GetUserRank(User.Id, GetCurrentGameMode());

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

    public GameMode GetCurrentGameMode()
    {
        return Status.PlayMode;
    }

    public BanchoUserStatus GetPlayerStatus()
    {
        return Status;
    }
}