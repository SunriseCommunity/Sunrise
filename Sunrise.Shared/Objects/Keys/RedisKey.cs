using Sunrise.Shared.Enums;
using Sunrise.Shared.Enums.Users;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Objects.Keys;

public static class RedisKey
{
// @formatter:off

    // Primitives
    public static string ApiServerRateLimited(ApiServer server) { return $"api:{(int)server}:ratelimited"; }

    // Objects
    public static string BeatmapSetBySetId(int id) { return $"beatmapset:set_id:{id}"; }
    public static string LocationFromIp(string ipAddress) { return $"location:{ipAddress}"; }
    
    // Pointers
    public static string BeatmapSetIdByHash(string hash) { return $"beatmapset_id:beatmap_hash:{hash}"; }
    public static string BeatmapSetIdByBeatmapId(int id) { return $"beatmapset_id:beatmap_id:{id}"; }

    // Sorted Set
    public static string LeaderboardGlobal(GameMode mode) { return $"leaderboard:global:{(int)mode}"; }
    public static string LeaderboardCountry(GameMode mode, CountryCode countryCode) { return $"leaderboard:{(int)countryCode}:{(int)mode}"; }
}