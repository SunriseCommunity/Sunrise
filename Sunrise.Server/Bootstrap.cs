using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.FileProviders;
using Prometheus;
using Sunrise.API.Controllers;
using Sunrise.Server.Repositories;
using Sunrise.Server.Repositories.Attributes;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Types.Interfaces;

namespace Sunrise.Server;

public static class Bootstrap
{
    public static void Configure(this WebApplicationBuilder builder)
    {
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(AuthController).Assembly)
            .AddApplicationPart(typeof(BaseController).Assembly)
            .AddApplicationPart(typeof(BeatmapController).Assembly)
            .AddApplicationPart(typeof(ScoreController).Assembly)
            .AddApplicationPart(typeof(UserController).Assembly);

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

    public static void AddSingletons(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<RedisRepository>();
        builder.Services.AddSingleton<DatabaseManager>();

        builder.Services.AddSingleton<ISessionRepository, SessionRepository>();
        builder.Services.AddSingleton<ChannelRepository>();
        builder.Services.AddSingleton<RateLimitRepository>();
        builder.Services.AddSingleton<MatchRepository>();
    }

    public static void WarmUpSingletons(this WebApplication app)
    {
        app.Services.GetRequiredService<RedisRepository>();
        app.Services.GetRequiredService<DatabaseManager>();

        app.Services.GetRequiredService<ISessionRepository>();
        app.Services.GetRequiredService<ChannelRepository>();
        app.Services.GetRequiredService<RateLimitRepository>();
        app.Services.GetRequiredService<MatchRepository>();
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
        CommandRepository.GetHandlers();
        PacketRepository.GetHandlers();

        ServicesProviderHolder.ServiceProvider = app.Services;
        Configuration.Initialize();
    }
}