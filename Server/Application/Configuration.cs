using Sunrise.Server.Database;
using Sunrise.Server.Objects;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Application;

public static class Configuration
{
    public const string DataPath = "./Data/";
    public const string DatabaseName = "sunrise.db";
    private static readonly string? Env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

    private static readonly IConfigurationRoot Config = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", false).AddJsonFile($"appsettings.{Env}.json", false).Build();

    // API section
    private static string? _webTokenSecret;
    public static bool IsDevelopment => Env == "Development";

    public static string WebTokenSecret
    {
        get { return _webTokenSecret ??= GetApiToken().ToHash(); }
    }

    public static TimeSpan WebTokenExpiration =>
        TimeSpan.FromSeconds(Config.GetSection("API").GetValue<int?>("TokenExpiresIn") ?? 3600);

    public static int ApiCallsPerWindow =>
        Config.GetSection("API").GetSection("RateLimit").GetValue<int?>("CallsPerWindow") ?? 100;

    public static int ApiWindow =>
        Config.GetSection("API").GetSection("RateLimit").GetValue<int?>("Window") ?? 10;

    // General section
    public static string WelcomeMessage => Config.GetSection("General").GetValue<string?>("WelcomeMessage") ?? "";
    public static string Domain => Config.GetSection("General").GetValue<string?>("WebDomain") ?? "";
    public static string MedalMirrorUrl => Config.GetSection("General").GetValue<string?>("MedalMirrorUrl") ?? "";

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

    public static string[] BannedIps => Config.GetSection("General").GetSection("BannedIps").Get<string[]>() ?? [];

    // Bot section
    public static string BotUsername => Config.GetSection("Bot").GetValue<string?>("Username") ?? "";
    public static string BotPrefix => Config.GetSection("Bot").GetValue<string?>("Prefix") ?? "";

    // Redis section
    public static string RedisConnection => Config.GetSection("Redis").GetValue<string?>("ConnectionString") ?? "";
    public static int RedisCacheLifeTime => Config.GetSection("Redis").GetValue<int?>("CacheLifeTime") ?? 300;
    public static bool UseCache => Config.GetSection("Redis").GetValue<bool?>("UseCache") ?? true;

    public static bool ClearCacheOnStartup =>
        Config.GetSection("Redis").GetValue<bool?>("ClearCacheOnStartup") ?? false;

    // Hangfire section
    public static string HangfireConnection =>
        Config.GetSection("Hangfire").GetValue<string?>("ConnectionString") ?? "";

    public static int MaxDailyBackupCount =>
        Config.GetSection("Hangfire").GetValue<int?>("MaxDailyBackupCount") ?? 3;

    public static string ObservatoryApiKey =>
        Config.GetSection("General").GetValue<string?>("ObservatoryApiKey") ?? "";

    private static string ObservatoryUrl =>
        Config.GetSection("General").GetValue<string?>("ObservatoryUrl") ?? "";

    public static List<ExternalApi> ExternalApis { get; } = [];

    public static void Initialize()
    {
        EnsureBotExists();
        AddObservatoryUrls();
    }

    private static void AddObservatoryUrls()
    {
        if (string.IsNullOrEmpty(ObservatoryUrl)) throw new Exception("Observatory URL is empty. Please check your configuration. Check README if you have issues with setting up Observatory.");
        
        ExternalApis.AddRange([
            new ExternalApi(ApiType.BeatmapDownload, ApiServer.Observatory, $"http://{ObservatoryUrl}/osu/{{0}}", 0, 1),
            new ExternalApi(ApiType.BeatmapSetDataById, ApiServer.Observatory, $"http://{ObservatoryUrl}/api/v2/s/{{0}}", 0, 1),
            new ExternalApi(ApiType.BeatmapSetDataByBeatmapId, ApiServer.Observatory, $"http://{ObservatoryUrl}/api/v2/b/{{0}}?full=true", 0, 1),
            new ExternalApi(ApiType.BeatmapSetDataByHash, ApiServer.Observatory, $"http://{ObservatoryUrl}/api/v2/md5/{{0}}?full=true", 0, 1),
            new ExternalApi(ApiType.BeatmapSetSearch,
                ApiServer.Observatory,
                $"http://{ObservatoryUrl}/api/v2/search?query={{0}}&limit={{1}}&offset={{2}}&status={{3}}&mode={{4}}",
                0,
                3)
        ]);
    }

    private static void EnsureBotExists()
    {
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        database.InitializeBotInDatabase().Wait();
    }

    private static string GetApiToken()
    {
        var apiToken = Config.GetSection("API").GetValue<string?>("TokenSecret");
        if (string.IsNullOrEmpty(apiToken)) throw new Exception("API token is empty. Please check your configuration.");
        
        

        return apiToken;
    }
}