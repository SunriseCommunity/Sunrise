using osu.Shared;

namespace Sunrise.Server.Types.Enums;

public static class RedisKey
{
    // Primitives
    public static string UserRateLimit(int userId) => $"ratelimit:user:{userId}";
    public static string ApiServerRateLimited(ApiServer server) => $"api:{(int)server}:ratelimited";
    public static string OsuVersion(string version) => $"osu:version:{version}"; 

    // Objects
    public static string UserById(int userId) => $"user:id:{userId}";
    public static string UserByUsername(string username) => $"user:username:{username}";
    public static string UserByEmail(string email) => $"user:email:{email}";
    public static string UserStats(int userId, GameMode mode) => $"user:{userId}:stats:{(int)mode}";
    public static string BeatmapSetByHash(string hash) => $"beatmapset:hash:{hash}";
    public static string BeatmapSetByBeatmapId(int id) => $"beatmapset:beatmap:{id}";
    public static string BeatmapSetBySetId(int  id) => $"beatmapset:set:{id}";
    public static string Score(int scoreId) => $"score:{scoreId}";
    public static string Scores(string id, string type) => $"scores:{id}:leaderboardtype:{type}";
    public static string BeatmapSearch(string search) => $"beatmapset:serach:{search}";

    // Records (Includes file paths)
    public static string BeatmapRecord(int beatmapId) => $"beatmap:{beatmapId}";
    public static string AvatarRecord(int userId) => $"avatar:{userId}";
    public static string BannerRecord(int userId) => $"banner:{userId}";
    public static string ReplayRecord(int replayId) => $"replay:{replayId}";
    
    // Sorted Set
    public static string LeaderboardGlobal(GameMode mode) => $"leaderboard:global:{(int)mode}";
}    
    