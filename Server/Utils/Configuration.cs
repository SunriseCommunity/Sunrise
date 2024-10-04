using Sunrise.Server.Database;
using Sunrise.Server.Objects;
using Sunrise.Server.Types.Enums;

namespace Sunrise.Server.Utils;

public static class Configuration
{
    private static readonly string? Env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

    private static readonly IConfigurationRoot Config = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", false).AddJsonFile($"appsettings.{Env}.json", false).Build();

    // API section
    private static string? _webTokenSecret;

    public static string WebTokenSecret
    {
        get { return _webTokenSecret ??= GetApiToken().ToHash(); }
    }

    public static TimeSpan WebTokenExpiration =>
        TimeSpan.FromSeconds(Config.GetSection("API").GetValue<int?>("TokenExpiresIn") ?? 3600);

    //public static int UserApiCallsInMinute => 60; // REDO ON WINDOW SYSTEM

    public static int ApiCallsPerWindow =>
        Config.GetSection("API").GetSection("RateLimit").GetValue<int?>("CallsPerWindow") ?? 100;

    public static int ApiWindow =>
        Config.GetSection("API").GetSection("RateLimit").GetValue<int?>("Window") ?? 10;

    // General section
    public static string WelcomeMessage => Config.GetSection("General").GetValue<string?>("WelcomeMessage") ?? "";
    public static string Domain => Config.GetSection("General").GetValue<string?>("WebDomain") ?? "";

    public static int GeneralCallsPerWindow =>
        Config.GetSection("General").GetSection("RateLimit").GetValue<int?>("CallsPerWindow") ?? 100;

    public static int GeneralWindow =>
        Config.GetSection("General").GetSection("RateLimit").GetValue<int?>("Window") ?? 10;

    public static bool OnMaintenance { get; set; } =
        Config.GetSection("General").GetValue<bool?>("OnMaintenance") ?? false;

    public static bool IncludeUserTokenInLogs =>
        Config.GetSection("General").GetValue<bool?>("IncludeUserTokenInLogs") ?? false;

    public static bool IgnoreBeatmapRanking =>
        Config.GetSection("General").GetValue<bool?>("IgnoreBeatmapRanking") ?? false;

    public static string[] BannedIps => Config.GetSection("General").GetValue<string[]>("BannedIps") ?? [];

    // Bot section
    public static string BotUsername => Config.GetSection("Bot").GetValue<string?>("Username") ?? "";
    public static string BotPrefix => Config.GetSection("Bot").GetValue<string?>("Prefix") ?? "";

    // Redis section
    public static string RedisConnection => Config.GetSection("Redis").GetValue<string?>("ConnectionString") ?? "";
    public static int RedisCacheLifeTime => Config.GetSection("Redis").GetValue<int?>("CacheLifeTime") ?? 300;

    public static List<ExternalApi> ExternalApis { get; } =
    [
        new(ApiType.BeatmapDownload, ApiServer.OldPpy, "https://old.ppy.sh/osu/{0}", 0, 1),

        new(ApiType.BeatmapSetSearch, ApiServer.CatboyBest,
            "https://catboy.best/api/v2/search?query={0}&limit={1}&offset={2}&status={3}&mode={4}", 1, 3),

        new(ApiType.BeatmapDownload, ApiServer.OsuDirect, "https://osu.direct/api/osu/{0}", 2, 1),
        new(ApiType.BeatmapSetDataById, ApiServer.OsuDirect, "https://osu.direct/api/v2/s/{0}", 0, 1),
        new(ApiType.BeatmapSetDataByBeatmapId, ApiServer.OsuDirect, "https://osu.direct/api/v2/b/{0}?full=true", 0, 1),
        new(ApiType.BeatmapSetDataByHash, ApiServer.OsuDirect, "https://osu.direct/api/v2/md5/{0}?full=true", 0, 1),
        new(ApiType.BeatmapSetSearch, ApiServer.OsuDirect,
            "https://osu.direct/api/v2/search/?q={0}&amount={1}&offset={2}&status={3}&mode={4}", 0, 3),

        new(ApiType.BeatmapsByBeatmapIds, ApiServer.Nerinyan,
            "https://proxy.nerinyan.moe/search?option=mapId&s=-2,-1,0,1,2,3,4&q={0}", 0, 1)
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

    private static string GetApiToken()
    {
        var apiToken = Config.GetSection("API").GetValue<string?>("Token");
        if (string.IsNullOrEmpty(apiToken)) throw new Exception("API token is empty. Please check your configuration.");
        return apiToken;
    }
}