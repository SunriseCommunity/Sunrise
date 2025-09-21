using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Objects;

namespace Sunrise.Shared.Application;

public static class Configuration
{
    private static readonly string? Env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

    private static readonly IConfigurationRoot Config = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", false)
        .AddJsonFile($"appsettings.{Env}.json", true)
        .AddEnvironmentVariables()
        .Build();

    // API section
    private static string? _webTokenSecret;

    public static JsonSerializerOptions SystemTextJsonOptions = new()
    {
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    public static TokenValidationParameters WebTokenValidationParameters = new()
    {
        ValidIssuer = "Sunrise",
        ValidAudience = "Sunrise",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(WebTokenSecret)),
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.Zero
    };

    public static bool IsDevelopment => Env == "Development";
    public static bool IsTestingEnv => Env == "Tests";

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

    public static string ApiDocumentationPath =>
        Config.GetSection("API").GetValue<string?>("DocumentationPath") ?? "/docs";

    // Database section
    public static string DatabaseConnectionString => Config.GetSection("Database").GetValue<string?>("ConnectionString") ?? "";

    // Files section
    private static string _dataPath => Config.GetSection("Files").GetValue<string?>("DataPath") ?? "";
    public static string DataPath => _dataPath.StartsWith('.') ? Path.Combine(Directory.GetCurrentDirectory(), _dataPath) : _dataPath;
    public static string BannedUsernamesName => Config.GetSection("Files").GetValue<string?>("BannedUsernamesName") ?? "";


    // General section
    public static string WelcomeMessage => Config.GetSection("General").GetValue<string?>("WelcomeMessage") ?? "";
    public static string Domain => Config.GetSection("General").GetValue<string?>("WebDomain") ?? "";
    public static string MedalMirrorUrl => Config.GetSection("General").GetValue<string?>("MedalMirrorUrl") ?? "";

    public static int GeneralCallsPerWindow =>
        Config.GetSection("General").GetSection("RateLimit").GetValue<int?>("CallsPerWindow") ?? 100;

    public static int CountryChangeCooldownInDays =>
        Config.GetSection("General").GetValue<int?>("CountryChangeCooldownInDays") ?? 90;

    public static int UsernameChangeCooldownInDays =>
        Config.GetSection("General").GetValue<int?>("UsernameChangeCooldownInDays") ?? 30;

    public static int GeneralWindow =>
        Config.GetSection("General").GetSection("RateLimit").GetValue<int?>("Window") ?? 10;

    public static int QueueLimit =>
        Config.GetSection("General").GetSection("RateLimit").GetValue<int?>("QueueLimit") ?? 15;

    public static bool OnMaintenance { get; set; } =
        Config.GetSection("General").GetValue<bool?>("OnMaintenance") ?? false;

    public static bool IncludeUserTokenInLogs =>
        Config.GetSection("General").GetValue<bool?>("IncludeUserTokenInLogs") ?? false;

    public static bool IgnoreBeatmapRanking =>
        Config.GetSection("General").GetValue<bool?>("IgnoreBeatmapRanking") ?? false;

    public static bool UseCustomBackgrounds => Config.GetSection("General").GetValue<bool?>("UseCustomBackgrounds") ?? false;


    // - Will use best scores by performance points instead of total score for performance calculation
    public static bool UseNewPerformanceCalculationAlgorithm =>
        Config.GetSection("General").GetValue<bool?>("UseNewPerformanceCalculationAlgorithm") ?? false;

    // Beatmap hype
    public static int UserHypesWeekly =>
        Config.GetSection("BeatmapHype").GetValue<int?>("UserHypesWeekly") ?? 6;

    public static int HypesToStartHypeTrain =>
        Config.GetSection("BeatmapHype").GetValue<int?>("HypesToStartHypeTrain") ?? 3;

    public static bool AllowMultipleHypeFromSameUser =>
        Config.GetSection("BeatmapHype").GetValue<bool?>("AllowMultipleHypeFromSameUser") ?? true;


    // Moderation section
    public static int BannablePpThreshold => Config.GetSection("Moderation").GetSection("BannablePPThreshold").Get<int?>() ?? 3000;
    public static string[] BannedIps => Config.GetSection("Moderation").GetSection("BannedIps").Get<string[]>() ?? [];


    // Bot section
    public static string BotUsername => Config.GetSection("Bot").GetValue<string?>("Username") ?? "";
    public static string BotPrefix => Config.GetSection("Bot").GetValue<string?>("Prefix") ?? "";

    // Redis section
    public static string RedisConnection => Config.GetSection("Redis").GetValue<string?>("ConnectionString") ?? "";
    public static int RedisCacheLifeTime => Config.GetSection("Redis").GetValue<int?>("CacheLifeTime") ?? 300;
    public static bool UseCache => Config.GetSection("Redis").GetValue<bool?>("UseCache") ?? false;
    public static bool UseRedisAsSecondCachingForDatabase => Config.GetSection("Redis").GetValue<bool?>("UseRedisAsSecondCachingForDatabase") ?? true;

    public static bool ClearCacheOnStartup =>
        Config.GetSection("Redis").GetValue<bool?>("ClearCacheOnStartup") ?? false;

    // Hangfire section
    public static bool UseHangfireServer =>
        Config.GetSection("Hangfire").GetValue<bool?>("UseHangfireServer") ?? false;

    public static string HangfireConnection =>
        Config.GetSection("Hangfire").GetValue<string?>("ConnectionString") ?? "";

    public static int MaxDailyBackupCount =>
        Config.GetSection("Hangfire").GetValue<int?>("MaxDailyBackupCount") ?? 3;

    public static string ObservatoryApiKey =>
        Config.GetSection("General").GetValue<string?>("ObservatoryApiKey") ?? "";

    private static string ObservatoryUrl =>
        Config.GetSection("General").GetValue<string?>("ObservatoryUrl") ?? "";

    public static List<ExternalApi> ExternalApis { get; } =
    [
        new(ApiType.GetIPLocation, ApiServer.IpApi, "http://ip-api.com/json/{0}", 0, 1)
    ];

    public static IConfigurationRoot GetConfig()
    {
        return Config;
    }

    public static void Initialize()
    {
        AddObservatoryUrls();
    }

    private static void AddObservatoryUrls()
    {
        if (string.IsNullOrEmpty(ObservatoryUrl)) throw new Exception("Observatory URL is empty. Please check your configuration. Check README if you have issues with setting up Observatory.");

        ExternalApis.AddRange([
            new ExternalApi(ApiType.CalculateBeatmapPerformance, ApiServer.Observatory, $"http://{ObservatoryUrl}/calculator/beatmap/{{0}}?acc={{1}}&mode={{2}}&mods={{3}}&combo={{4}}&misses={{5}}", 0, 1),
            new ExternalApi(ApiType.CalculateScorePerformance, ApiServer.Observatory, $"http://{ObservatoryUrl}/calculator/score", 0, 0, true),
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

    private static string GetApiToken()
    {
        var apiToken = Config.GetSection("API").GetValue<string?>("TokenSecret");
        if (string.IsNullOrEmpty(apiToken)) throw new Exception("API token is empty. Please check your configuration.");

        return apiToken;
    }
}