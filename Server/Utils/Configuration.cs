using Sunrise.Server.Database;
using Sunrise.Server.Objects;
using Sunrise.Server.Types.Enums;

namespace Sunrise.Server.Utils;

public static class Configuration
{
    public static bool IsDevelopment => Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

    public static bool IgnoreBeatmapRanking => true;
    public static string RedisConnection => IsDevelopment ? "localhost:6379" : "redis";
    public static string WelcomeMessage => "Welcome to Sunrise!";
    public static string BotUsername => "Sunshine Bot";
    public static string BotPrefix => "!";
    public static string Domain => IsDevelopment ? "sunrise.local" : "osu-sunrise.top";
    public static bool OnMaintenance { get; set; }
    public static int UserApiCallsInMinute => 60;
    public static int ServerRateLimit => 100;
    public static int ServerRateLimitWindow => 10;
    public static bool IncludeUserTokenInLogs => false;
    public static DateTime WebTokenExpiration => DateTime.UtcNow.AddHours(1);
    public static string WebTokenSecret => "VerySafeTokenQuestion".ToHash();

    public static string[] BannedIps => [];

    public static List<ExternalApi> ExternalApis { get; } =
    [
        new ExternalApi(ApiType.BeatmapDownload, ApiServer.OldPpy, "https://old.ppy.sh/osu/{0}", 0, 1),

        new ExternalApi(ApiType.BeatmapSetSearch, ApiServer.CatboyBest, "https://catboy.best/api/v2/search?query={0}&limit={1}&offset={2}&status={3}&mode={4}", 1, 3),

        new ExternalApi(ApiType.BeatmapDownload, ApiServer.OsuDirect, "https://osu.direct/api/osu/{0}", 2, 1),
        new ExternalApi(ApiType.BeatmapSetDataById, ApiServer.OsuDirect, "https://osu.direct/api/v2/s/{0}", 0, 1),
        new ExternalApi(ApiType.BeatmapSetDataByBeatmapId, ApiServer.OsuDirect, "https://osu.direct/api/v2/b/{0}?full=true", 0, 1),
        new ExternalApi(ApiType.BeatmapSetDataByHash, ApiServer.OsuDirect, "https://osu.direct/api/v2/md5/{0}?full=true", 0, 1),
        new ExternalApi(ApiType.BeatmapSetSearch, ApiServer.OsuDirect, "https://osu.direct/api/v2/search/?q={0}&amount={1}&offset={2}&status={3}&mode={4}", 0, 3),

        new ExternalApi(ApiType.BeatmapsByBeatmapIds, ApiServer.Nerinyan, "https://proxy.nerinyan.moe/search?option=mapId&s=-2,-1,0,1,2,3,4&q={0}", 0, 1)
    ];

    public static void Initialize()
    {
        EnsureBotExists();
    }

    private static void EnsureBotExists()
    {
        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();
        database.InitializeBotInDatabase().Wait();
    }
}