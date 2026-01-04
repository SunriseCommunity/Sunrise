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
    public static string DatabaseConnectionString =>
        GetValuesFromEnvOrFallbackToDeprecatedConfigIfCantAccessEnv("MYSQL_HOST",
            () => string.Format("Host={0};Port={1};Database={2};Username={3};Password={4};SslMode=Required;",
                Environment.GetEnvironmentVariable("MYSQL_HOST") ?? "localhost",
                Environment.GetEnvironmentVariable("MYSQL_PORT") ?? "3306",
                Environment.GetEnvironmentVariable("MYSQL_DATABASE") ?? "sunrise",
                Environment.GetEnvironmentVariable("MYSQL_USER") ?? "root",
                Environment.GetEnvironmentVariable("MYSQL_PASSWORD") ?? "root"
            ),
            () => Config.GetSection("Database").GetValue<string?>("ConnectionString"));

    public static int SlowQueryThresholdMilliseconds =>
        Config.GetSection("Database").GetValue<int?>("SlowQueryThresholdMilliseconds") ?? 1_000;

    // Files section
    private static string _dataPath => Config.GetSection("Files").GetValue<string?>("DataPath") ?? "";
    public static string DataPath => _dataPath.StartsWith('.') ? Path.Combine(Directory.GetCurrentDirectory(), _dataPath) : _dataPath;
    public static string BannedUsernamesName => Config.GetSection("Files").GetValue<string?>("BannedUsernamesName") ?? "";


    // General section
    public static string WelcomeMessage => Config.GetSection("General").GetValue<string?>("WelcomeMessage") ?? "";

    public static string Domain => GetValuesFromEnvOrFallbackToDeprecatedConfigIfCantAccessEnv("WEB_DOMAIN",
        () => Environment.GetEnvironmentVariable("WEB_DOMAIN"),
        () => Config.GetSection("General").GetValue<string?>("WebDomain"));

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


    // Telemetry section
    public static bool UseMetrics => Config.GetSection("Telemetry").GetValue<bool?>("UseMetrics") ?? true;

    public static string TempoUri => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEMPO_HOST"))
        ? string.Format("http://{0}:{1}",
            Environment.GetEnvironmentVariable("TEMPO_HOST") ?? "localhost",
            Environment.GetEnvironmentVariable("TEMPO_PORT") ?? "4317")
        : "";

    public static string LokiUri => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LOKI_HOST"))
        ? string.Format("http://{0}:{1}",
            Environment.GetEnvironmentVariable("LOKI_HOST") ?? "localhost",
            Environment.GetEnvironmentVariable("LOKI_PORT") ?? "3100")
        : "";

    public static bool UseW3CFileLogging =>
        Config.GetSection("Telemetry").GetSection("Logging").GetValue<bool?>("UseW3CFileLogging") ?? false;

    // Moderation section
    public static int BannablePpThreshold => Config.GetSection("Moderation").GetSection("BannablePPThreshold").Get<int?>() ?? 3000;
    public static string[] BannedIps => Config.GetSection("Moderation").GetSection("BannedIps").Get<string[]>() ?? []; // TODO: Need to deprecate this later


    // Bot section
    public static string BotUsername => Config.GetSection("Bot").GetValue<string?>("Username") ?? "";
    public static string BotPrefix => Config.GetSection("Bot").GetValue<string?>("Prefix") ?? "";

    // Redis section
    public static string RedisConnection => GetValuesFromEnvOrFallbackToDeprecatedConfigIfCantAccessEnv("REDIS_HOST",
        () => string.Format("{0}:{1}",
            Environment.GetEnvironmentVariable("REDIS_HOST") ?? throw new Exception("REDIS_HOST environment variable is not set."),
            Environment.GetEnvironmentVariable("REDIS_PORT") ?? throw new Exception("REDIS_PORT environment variable is not set.")
        ),
        () => Config.GetSection("Redis").GetValue<string?>("ConnectionString"));

    public static int RedisCacheLifeTime => Config.GetSection("Redis").GetValue<int?>("CacheLifeTime") ?? 300;
    public static bool UseCache => Config.GetSection("Redis").GetValue<bool?>("UseCache") ?? false;
    public static bool UseRedisAsSecondCachingForDatabase => Config.GetSection("Redis").GetValue<bool?>("UseRedisAsSecondCachingForDatabase") ?? true;

    public static bool ClearCacheOnStartup =>
        Config.GetSection("Redis").GetValue<bool?>("ClearCacheOnStartup") ?? false;

    // Hangfire section
    public static bool UseHangfireServer =>
        Config.GetSection("Hangfire").GetValue<bool?>("UseHangfireServer") ?? false;

    public static string HangfireMysqlConnection => GetValuesFromEnvOrFallbackToDeprecatedConfigIfCantAccessEnv("HANGFIRE_HOST",
        () => string.Format("server={0};port={1};user={2};password={3}",
            Environment.GetEnvironmentVariable("HANGFIRE_HOST") ?? throw new Exception("HANGFIRE_HOST environment variable is not set."),
            Environment.GetEnvironmentVariable("HANGFIRE_PORT") ?? throw new Exception("HANGFIRE_PORT environment variable is not set."),
            Environment.GetEnvironmentVariable("HANGFIRE_USER") ?? throw new Exception("HANGFIRE_USER environment variable is not set."),
            Environment.GetEnvironmentVariable("HANGFIRE_PASSWORD") ?? throw new Exception("HANGFIRE_PASSWORD environment variable is not set.")
        ),
        () => throw new Exception("Deprecated hangfire connection was using Postgres, which is no longer supported. Please set up Hangfire connection using environment variables."));

    public static int MaxDailyBackupCount =>
        Config.GetSection("Hangfire").GetValue<int?>("MaxDailyBackupCount") ?? 3;

    public static string ObservatoryApiKey =>
        GetValuesFromEnvOrFallbackToDeprecatedConfigIfCantAccessEnv("OBSERVATORY_API_KEY",
            () => Environment.GetEnvironmentVariable("OBSERVATORY_API_KEY"),
            () => Config.GetSection("General").GetValue<string?>("ObservatoryApiKey"));

    private static string ObservatoryUrl => GetValuesFromEnvOrFallbackToDeprecatedConfigIfCantAccessEnv("OBSERVATORY_HOST",
        () => string.Format("{0}:{1}",
            Environment.GetEnvironmentVariable("OBSERVATORY_HOST") ?? throw new Exception("OBSERVATORY_HOST environment variable is not set."),
            Environment.GetEnvironmentVariable("OBSERVATORY_PORT") ?? throw new Exception("OBSERVATORY_PORT environment variable is not set.")
        ),
        () => Config.GetSection("General").GetValue<string?>("ObservatoryUrl"));

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
            new ExternalApi(ApiType.BeatmapSetDataById, ApiServer.Observatory, $"http://{ObservatoryUrl}/api/v2/s/{{0}}?allowMissingNonBeatmapValues=true", 0, 1),
            new ExternalApi(ApiType.BeatmapSetDataByBeatmapId, ApiServer.Observatory, $"http://{ObservatoryUrl}/api/v2/b/{{0}}?full=true", 0, 1),
            new ExternalApi(ApiType.BeatmapSetDataByHash, ApiServer.Observatory, $"http://{ObservatoryUrl}/api/v2/md5/{{0}}?full=true&allowMissingNonBeatmapValues=true", 0, 1),
            new ExternalApi(ApiType.BeatmapSetsDataByBeatmapIds, ApiServer.Observatory, $"http://{ObservatoryUrl}/api/v2/beatmapsets?beatmapIds={{0}}", 0, 1),
            new ExternalApi(ApiType.BeatmapSetSearch,
                ApiServer.Observatory,
                $"http://{ObservatoryUrl}/api/v2/search?query={{0}}&limit={{1}}&offset={{2}}&status={{3}}&mode={{4}}",
                0,
                3)
        ]);
    }

    private static string GetApiToken()
    {
        var apiToken = GetValuesFromEnvOrFallbackToDeprecatedConfigIfCantAccessEnv("API_TOKEN_SECRET",
            () => Environment.GetEnvironmentVariable("API_TOKEN_SECRET"),
            () => Config.GetSection("API").GetValue<string?>("TokenSecret"));

        if (string.IsNullOrEmpty(apiToken)) throw new Exception("API token is empty. Please check your configuration.");

        return apiToken;
    }

    private static string GetValuesFromEnvOrFallbackToDeprecatedConfigIfCantAccessEnv(string envKey, Func<string?> envBasedFunc, Func<string?> deprecatedConfigFunc)
    {
        var envValue = Environment.GetEnvironmentVariable(envKey);
        if (string.IsNullOrEmpty(envValue))
            return deprecatedConfigFunc() ?? "";

        return envBasedFunc() ?? "";
    }
}