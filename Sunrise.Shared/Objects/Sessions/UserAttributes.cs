using HOPEless.Bancho.Objects;
using Microsoft.Extensions.DependencyInjection;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Objects.Serializable;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Objects.Sessions;

public class UserAttributes
{
    public UserAttributes(User user, Location location, LoginRequest loginRequest, bool usesOsuClient = true)
    {
        Latitude = location.Latitude;
        Longitude = location.Longitude;
        Timezone = location.TimeOffset;
        Country = Enum.TryParse(location.Country, out CountryCode country)
            ? (short)country != 0 ? (short)country : null
            : null;
        UserId = user.Id;
        UsesOsuClient = usesOsuClient;

        OsuVersion = loginRequest.Version;
        UserHash = loginRequest.ClientHash;
    }

    private int UserId { get; }
    public int Timezone { get; }
    private float Longitude { get; }
    private float Latitude { get; }
    private short? Country { get; }
    public string OsuVersion { get; }
    public string UserHash { get; }
    public DateTime LastPingRequest { get; private set; } = DateTime.UtcNow;
    public BanchoUserStatus Status { get; set; } = new();
    public bool ShowUserLocation { get; set; } = true;
    public bool IgnoreNonFriendPm { get; set; }
    public string? AwayMessage { get; set; }
    public bool IsBot { get; set; }
    public bool UsesOsuClient { get; set; }

    public async Task<BanchoUserPresence> GetPlayerPresence()
    {
        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var user = await database.Users.GetUser(id: UserId, options: new QueryOptions(true));
        if (user == null)
            throw new ApplicationException($"User with id {UserId} not found");
        
        var (globalRank, _) = await database.Users.Stats.Ranks.GetUserRanks(user, GetCurrentGameMode());
        var userRank = IsBot ? 0 : globalRank;

        return new BanchoUserPresence
        {
            UserId = user.Id,
            Username = user.Username,
            Timezone = Timezone,
            Latitude = ShowUserLocation ? Latitude : 0,
            Longitude = ShowUserLocation ? Longitude : 0,
            CountryCode = byte.Parse((Country ?? (short)user.Country).ToString()),
            Permissions = user.GetPrivilegeRank(), // FIXME: Chat color doesn't work
            Rank = (int)userRank,
            PlayMode = GetCurrentGameMode().ToVanillaGameMode(),
            UsesOsuClient = UsesOsuClient
        };
    }

    public async Task<BanchoUserData> GetPlayerData()
    {
        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();
        
        var user = await database.Users.GetUser(id: UserId, options: new QueryOptions(true));
        if (user == null)
            throw new ApplicationException($"User with id {UserId} not found");

        var userStats = IsBot ? new UserStats() : await database.Users.Stats.GetUserStats(user.Id, GetCurrentGameMode());
        if (userStats == null)
            throw new ApplicationException($"User stats for user with id {UserId} not found");

        var (globalRank, _) = await database.Users.Stats.Ranks.GetUserRanks(user, GetCurrentGameMode());
        var userRank = IsBot ? 0 : globalRank;

        // Note: osu! client expects short integer for performance points. So to avoid this limitation, we will send pp as ranked score if it's over the limit.
        var isPerformanceOverClientLimit = userStats.PerformancePoints > short.MaxValue;

        return new BanchoUserData
        {
            UserId = user.Id,
            Status = Status,
            Rank = (int)userRank,
            Performance = (short)(isPerformanceOverClientLimit ? 0 : userStats.PerformancePoints),
            Accuracy = (float)(userStats.Accuracy / 100f),
            Playcount = userStats.PlayCount,
            RankedScore = isPerformanceOverClientLimit ? (long)userStats.PerformancePoints : userStats.RankedScore,
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