using EFCoreSecondLevelCacheInterceptor;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Prometheus;
using Scalar.AspNetCore;
using StackExchange.Redis;
using Sunrise.API.Controllers;
using Sunrise.API.Managers;
using Sunrise.Server.Repositories;
using Sunrise.Server.Services;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Repositories;
using Sunrise.Shared.Database.Services;
using Sunrise.Shared.Database.Services.Events;
using Sunrise.Shared.Database.Services.Users;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Repositories.Multiplayer;
using Sunrise.Shared.Services;
using AssetService = Sunrise.API.Services.AssetService;
using AuthService = Sunrise.API.Services.AuthService;
using WebSocketManager = Sunrise.API.Managers.WebSocketManager;

namespace Sunrise.Server;

public static class Bootstrap
{
    public static void Configure(this WebApplicationBuilder builder)
    {
        builder.Services.AddEndpointsApiExplorer();

        builder.Services.AddProblemDetails();
        builder.Services.AddMetrics();


        builder.Services.AddW3CLogging(logging =>
        {
            logging.LoggingFields = W3CLoggingFields.All;
            logging.AdditionalRequestHeaders.Add("x-forwarded-for");
            logging.AdditionalRequestHeaders.Add("osu-version");
            if (Configuration.IncludeUserTokenInLogs) logging.AdditionalRequestHeaders.Add("osu-token");
        });
    }

    public static void AddApiDocs(this WebApplicationBuilder builder)
    {

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
    }


    public static void AddHangfire(this WebApplicationBuilder builder)
    {
        if (Configuration.UseHangfireServer)
        {
            builder.Services.AddHangfire(config => { config.UsePostgreSqlStorage(c => c.UseNpgsqlConnection(Configuration.HangfireConnection)); });
        }
        else
        {
            builder.Services.AddHangfire(config => config.UseInMemoryStorage());
        }

        builder.Services.AddHangfireServer();
    }

    public static void AddMiddlewares(this WebApplicationBuilder builder)
    {
        builder.Services.AddMemoryCache();
        builder.Services.AddTransient<Middleware>();

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy => policy
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod()
            );
        });

        builder.Services.AddRequestTimeouts(options =>
        {
            options.DefaultPolicy = new RequestTimeoutPolicy
            {
                Timeout = TimeSpan.FromSeconds(10),
                TimeoutStatusCode = StatusCodes.Status408RequestTimeout
            };
        });
    }

    public static void AddSunriseDbContextPool(this WebApplicationBuilder builder)
    {
        var isUseSecondLevelCache = Configuration.UseRedisAsSecondCachingForDatabase;

        if (isUseSecondLevelCache)
            builder.Services.AddEFSecondLevelCache(options =>
            {
                options.UseStackExchangeRedisCacheProvider(Configuration.RedisConnection, TimeSpan.FromSeconds(10))
                    .UseCacheKeyPrefix("EF_").ConfigureLogging(true);

                options.CacheAllQueries(CacheExpirationMode.Sliding, TimeSpan.FromMinutes(5));
                options.UseDbCallsIfCachingProviderIsDown(TimeSpan.FromMinutes(1));
            });

        builder.Services.AddDbContextPool<SunriseDbContext>((serviceProvider, optionsBuilder) =>
        {
            if (isUseSecondLevelCache)
                optionsBuilder.AddInterceptors(serviceProvider.GetRequiredService<SecondLevelCacheInterceptor>());

            optionsBuilder
                .UseMySQL(Configuration.DatabaseConnectionString);
        });
    }

    public static void AddSingletons(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<SessionRepository>();
        builder.Services.AddSingleton<ChatChannelRepository>();
        builder.Services.AddSingleton<RateLimitRepository>();
        builder.Services.AddSingleton<MatchRepository>();

        builder.Services.AddSingleton(ConnectionMultiplexer.Connect($"{Configuration.RedisConnection},allowAdmin=true"));

    }

    public static void AddApiEndpoints(this WebApplicationBuilder builder)
    {
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(AuthController).Assembly)
            .AddApplicationPart(typeof(BaseController).Assembly)
            .AddApplicationPart(typeof(BeatmapController).Assembly)
            .AddApplicationPart(typeof(ScoreController).Assembly)
            .AddApplicationPart(typeof(UserController).Assembly);

        builder.Services.AddSingleton<WebSocketManager>();

        builder.Services.AddScoped<AuthService>();
        builder.Services.AddScoped<AssetService>();
    }

    public static void AddDatabaseServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<RedisRepository>();

        builder.Services.AddScoped<DatabaseService>();

        builder.Services.AddScoped<BeatmapRepository>();

        builder.Services.AddScoped<MedalRepository>();

        builder.Services.AddScoped<UserRepository>();

        builder.Services.AddScoped<UserStatsService>();
        builder.Services.AddScoped<UserStatsSnapshotService>();
        builder.Services.AddScoped<UserStatsRanksService>();

        builder.Services.AddScoped<UserModerationService>();
        builder.Services.AddScoped<UserMedalsService>();
        builder.Services.AddScoped<UserFavouritesService>();
        builder.Services.AddScoped<UserFileService>();

        builder.Services.AddScoped<EventRepository>();
        builder.Services.AddScoped<UserEventService>();

        builder.Services.AddScoped<ScoreRepository>();
        builder.Services.AddScoped<ScoreFileService>();
    }


    public static void AddServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<DirectService>();
        builder.Services.AddScoped<MedalService>();
        builder.Services.AddScoped<Services.AssetService>();
        builder.Services.AddScoped<Services.AuthService>();
        builder.Services.AddScoped<BanchoService>();
        builder.Services.AddScoped<HttpClientService>();

        builder.Services.AddScoped<ScoreService>();
        builder.Services.AddScoped<UserService>();

        builder.Services.AddScoped<Services.AuthService>();
        builder.Services.AddScoped<SessionManager>();

        builder.Services.AddScoped<UserAuthService>();
        builder.Services.AddScoped<RegionService>();

        builder.Services.AddTransient<CalculatorService>();
        builder.Services.AddTransient<BeatmapService>();
        builder.Services.AddTransient(
            typeof(Lazy<>),
            typeof(LazilyResolved<>));
    }

    public static void WarmUpSingletons(this WebApplication app)
    {
        app.Services.GetRequiredService<SessionRepository>();
        app.Services.GetRequiredService<ChatChannelRepository>();
        app.Services.GetRequiredService<RateLimitRepository>();
        app.Services.GetRequiredService<MatchRepository>();
    }

    public static void UseScalarApiReference(this WebApplication app)
    {
        app.UseSwagger(options => { options.RouteTemplate = "/openapi/{documentName}.json"; });

        app.MapScalarApiReference("docs",
            options =>
            {
                options.Title = "Sunrise API Documentation";
                options.Theme = ScalarTheme.Mars;
            });
    }

    public static void ApplyDatabaseBootstrapping(this WebApplication app)
    {
        using var scope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        database.DbContext.Database.Migrate();
        DatabaseSeeder.UseAsyncSeeding(database.DbContext).Wait();
    }

    public static void UseStaticBackgrounds(this WebApplication app)
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider =
                new PhysicalFileProvider(Path.Combine(Configuration.DataPath, "Files/SeasonalBackgrounds")),
            RequestPath = "/static"
        });
    }

    public static void UseMiddlewares(this WebApplication app)
    {
        app.UseMiddleware<Middleware>();
        app.UseCors();
        app.UseRequestTimeouts();
    }

    public static void Configure(this WebApplication app)
    {
        app.UseRouting();
        app.UseW3CLogging();
        app.UseMetricServer().UseHttpMetrics();

#pragma warning disable ASP0014
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapMetrics();
        });
#pragma warning restore ASP0014
    }

    public static void Setup(this WebApplication app)
    {
        ChatCommandRepository.GetHandlers();
        PacketHandlerRepository.GetHandlers();

        ServicesProviderHolder.ServiceProvider = app.Services;
        Configuration.Initialize();
    }

    private class LazilyResolved<T> : Lazy<T>
    {
        public LazilyResolved(IServiceProvider serviceProvider)
            : base(serviceProvider.GetRequiredService<T>)
        {
        }
    }
}