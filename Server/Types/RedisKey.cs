using osu.Shared;
using Sunrise.Server.Types.Enums;

namespace Sunrise.Server.Types;

public static class RedisKey
{
    // @formatter:off

    // Primitives
    public static string ApiServerRateLimited(ApiServer server) { return $"api:{(int)server}:ratelimited"; }
    public static string OsuVersion(string version) { return $"osu:version:{version}"; }

    // Objects
    public static string UserById(int userId) { return $"user:id:{userId}"; }
    public static string UserByUsername(string username) { return $"user:username:{username}"; }
    public static string UserByUsernameAndPassHash(string username, string passhash) { return $"user:username:{username}:passhash:{passhash}"; }
    public static string UserByEmail(string email) { return $"user:email:{email}"; }
    public static string UserStats(int userId, GameMode mode) { return $"user:{userId}:stats:{(int)mode}"; }
    public static string AllUserStats(GameMode mode) { return $"user:all:stats:{(int)mode}"; }
    public static string AllUsers() { return "user:all"; }
    public static string BeatmapSetByHash(string hash) { return $"beatmapset:hash:{hash}"; }
    public static string BeatmapSetByBeatmapId(int id) { return $"beatmapset:beatmap:{id}"; }
    public static string BeatmapSetBySetId(int id) { return $"beatmapset:set:{id}"; }
    public static string Score(int scoreId) { return $"score:{scoreId}"; }
    public static string Scores(string id, string type) { return $"scores:{id}:leaderboardtype:{type}"; }
    public static string BeatmapSearch(string search) { return $"beatmapset:serach:{search}"; }

    // Records (Includes file paths)
    public static string BeatmapRecord(int beatmapId) { return $"beatmap:{beatmapId}"; }
    public static string AvatarRecord(int userId) { return $"avatar:{userId}"; }
    public static string BannerRecord(int userId) { return $"banner:{userId}"; }
    public static string ReplayRecord(int replayId) { return $"replay:{replayId}"; }
    public static string ScreenshotRecord(int screenshotId) { return $"screenshot:{screenshotId}"; }

    // Sorted Set
    public static string LeaderboardGlobal(GameMode mode) { return $"leaderboard:global:{(int)mode}"; }
}