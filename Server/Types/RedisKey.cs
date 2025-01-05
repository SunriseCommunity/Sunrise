using Sunrise.Server.Types.Enums;
using GameMode = Sunrise.Server.Types.Enums.GameMode;

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
    public static string BeatmapSetIdByHash(string hash) { return $"beatmapset_id:beatmap_hash:{hash}"; }
    public static string BeatmapSetIdByBeatmapId(int id) { return $"beatmapset_id:beatmap_id:{id}"; }
    
    public static string BeatmapSetBySetId(int id) { return $"beatmapset:set_id:{id}"; }
    public static string ScoreById(int scoreId) { return $"score:id:{scoreId}"; }
    public static string ScoreIdByScoreHash(string hash) { return $"score_id:score_hash:{hash}"; }
    public static string Scores(string id, string type) { return $"scores:{id}:leaderboardtype:{type}"; }
    public static string BeatmapSearch(string search) { return $"beatmapset:serach:{search}"; }
    public static string UserMedals(int userId, GameMode? mode = null) { return $"user:{userId}:{(mode.HasValue ? (int)mode : "all" )}:medals"; }
    public static string Medal(int medalId) { return $"medal:{medalId}"; }
    public static string AllMedals(GameMode mode) { return $"medal:all:{(int)mode}"; }
    public static string UserStatsSnapshot(int userId, GameMode mode) { return $"user:{userId}:stats:{(int)mode}:snapshot"; }

    // Records (Includes file paths)
    public static string BeatmapRecord(int beatmapId) { return $"beatmap:{beatmapId}"; }
    public static string AvatarRecord(int userId) { return $"avatar:{userId}"; }
    public static string BannerRecord(int userId) { return $"banner:{userId}"; }
    public static string ReplayRecord(int replayId) { return $"replay:{replayId}"; }
    public static string ScreenshotRecord(int screenshotId) { return $"screenshot:{screenshotId}"; }
    public static string MedalImageRecord(int medalId) { return $"medal:image:{medalId}"; }

    // Sorted Set
    public static string LeaderboardGlobal(GameMode mode) { return $"leaderboard:global:{(int)mode}"; }
    public static string LeaderboardCountry(GameMode mode, CountryCodes countryCode) { return $"leaderboard:{(int)countryCode}:{(int)mode}"; }
}