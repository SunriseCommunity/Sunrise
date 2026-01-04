using System.Diagnostics;
using System.Security.Authentication;
using System.Transactions;
using EFCoreSecondLevelCacheInterceptor;
using Hangfire;
using Hangfire.MySql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi.Models;
using MySql.Data.MySqlClient;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Exceptions;
using Serilog.Exceptions.Core;
using Serilog.Exceptions.EntityFrameworkCore.Destructurers;
using Serilog.Sinks.Grafana.Loki;
using StackExchange.Redis;
using Sunrise.API.Controllers;
using Sunrise.API.Serializable.Response;
using Sunrise.Server.Middlewares;
using Sunrise.Server.Repositories;
using Sunrise.Server.Services;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Interceptor;
using Sunrise.Shared.Database.Repositories;
using Sunrise.Shared.Database.Services;
using Sunrise.Shared.Database.Services.Beatmaps;
using Sunrise.Shared.Database.Services.Events;
using Sunrise.Shared.Database.Services.Users;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Repositories.Multiplayer;
using Sunrise.Shared.Services;
using Swashbuckle.AspNetCore.SwaggerGen;
using AssetService = Sunrise.API.Services.AssetService;
using AuthService = Sunrise.API.Services.AuthService;
using UserService = Sunrise.API.Services.UserService;
using WebSocketManager = Sunrise.API.Managers.WebSocketManager;

namespace Sunrise.Server;

public static class Bootstrap
{
    public static void Configure(this WebApplicationBuilder builder)
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddProblemDetails();
    }

    public static void AddCustomLogging(this WebApplicationBuilder builder)
    {
        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Extensions.Hosting", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.Hosting", LogEventLevel.Information)
            .Enrich.WithProperty("Application", "Sunrise")
            .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
            .Enrich.FromLogContext()
            .Enrich.With<TraceEnricher>()
            .Enrich.WithExceptionDetails(new DestructuringOptionsBuilder()
                .WithDefaultDestructurers()
                .WithDestructurers([new DbUpdateExceptionDestructurer()]))
            .WriteTo.Console(
                outputTemplate: "{Timestamp:HH:mm} [{Level}] ({ThreadId}) {Message}{NewLine}{Exception}");

        if (!string.IsNullOrEmpty(Configuration.LokiUri))
        {
            loggerConfiguration = loggerConfiguration.WriteTo.GrafanaLoki(
                Configuration.LokiUri,
                propertiesAsLabels: ["Application", "Environment"]);
        }

        var logger = loggerConfiguration.CreateLogger();

        Log.Logger = logger;

        builder.Host.UseSerilog(logger);
        builder.Services.AddLogging(b => b.AddSerilog(logger, true));

        if (Configuration.UseW3CFileLogging)
        {
            builder.Services.AddW3CLogging(logging =>
            {
                logging.LoggingFields = W3CLoggingFields.All;
                logging.AdditionalRequestHeaders.Add("x-forwarded-for");
                logging.AdditionalRequestHeaders.Add("osu-version");
                if (Configuration.IncludeUserTokenInLogs) logging.AdditionalRequestHeaders.Add("osu-token");
            });
        }
    }

    public static void AddTelemetry(this WebApplicationBuilder builder)
    {
        if (string.IsNullOrEmpty(Configuration.TempoUri) && Configuration.UseMetrics == false)
            return;

        var openTelemetryBuilder = builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resourceBuilder => resourceBuilder.AddService("Sunrise"));

        if (!string.IsNullOrEmpty(Configuration.TempoUri))
        {
            openTelemetryBuilder.WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        void EnrichWithHttpContext(Activity activity, HttpContext context)
                        {
                            var endpoint = context.GetEndpoint();
                            var path = (endpoint as RouteEndpoint)?.RoutePattern.RawText ?? context.Request.Path.Value;

                            var method = context.Request.Method;
                            var subdomain = context.Request.Host.Host.Split('.')[0];

                            activity.DisplayName = $"{method} {(string.IsNullOrWhiteSpace(subdomain) ? "" : $"{subdomain} ")}{path}";
                        }

                        options.EnrichWithHttpRequest = (activity, request) => EnrichWithHttpContext(activity, request.HttpContext);
                        options.EnrichWithHttpResponse = (activity, response) => EnrichWithHttpContext(activity, response.HttpContext);
                    })
                    .AddEntityFrameworkCoreInstrumentation()
                    .AddHangfireInstrumentation()
                    .AddRougamoSource()
                    .AddSource("*")
                    .AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(Configuration.TempoUri);
                        options.Protocol = OtlpExportProtocol.Grpc;
                    });
            });
        }

        if (Configuration.UseMetrics)
        {
            builder.Services.AddMetrics();

            openTelemetryBuilder.WithMetrics(metrics =>
            {
                metrics
                    .AddMeter("*")
                    .AddAspNetCoreInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddPrometheusExporter();
            });
        }
    }

    public static void AddApiDocs(this WebApplicationBuilder builder)
    {

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.EnableAnnotations();
            c.SupportNonNullableReferenceTypes();
            c.NonNullableReferenceTypesAsRequired();

            c.DocumentFilter<GenerateAdditionalOpenApiSchema>();

            c.AddJwtAuth();
        });

        builder.Services.AddControllersWithViews()
            .AddJsonOptions(options =>
            {
                foreach (var converter in Configuration.SystemTextJsonOptions.Converters)
                {
                    options.JsonSerializerOptions.Converters.Add(converter);
                }
            });
    }

    public static void AddAuthorizationPolicies(this WebApplicationBuilder builder)
    {
        builder.Services.AddAuthentication(x =>
        {
            x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            x.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(x => { x.TokenValidationParameters = Configuration.WebTokenValidationParameters; });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("RequireDeveloper", policy => policy.Requirements.Add(new UserPrivilegeRequirement(UserPrivilege.Developer)))
            .AddPolicy("RequireAdmin", policy => policy.Requirements.Add(new UserPrivilegeRequirement(UserPrivilege.Admin)))
            .AddPolicy("RequireBat", policy => policy.Requirements.Add(new UserPrivilegeRequirement(UserPrivilege.Bat)));

        builder.Services.AddScoped<IAuthorizationHandler, DatabaseAuthorizationHandler>();
        builder.Services.AddScoped<IAuthorizationMiddlewareResultHandler, CustomAuthorizationMiddlewareResultHandler>();
    }

    public static void AddHangfire(this WebApplicationBuilder builder)
    {
        if (Configuration.UseHangfireServer)
        {
            CreateHangfireDatabaseIfNotExists(Configuration.HangfireMysqlConnection);

            builder.Services.AddHangfire(config =>
            {
                config.UseStorage(new MySqlStorage($"{Configuration.HangfireMysqlConnection};database=hangfire",
                    new MySqlStorageOptions
                    {
                        TablesPrefix = "Hangfire_",
                        TransactionIsolationLevel = IsolationLevel.ReadCommitted,
                        QueuePollInterval = TimeSpan.FromSeconds(15),
                        JobExpirationCheckInterval = TimeSpan.FromHours(1),
                        CountersAggregateInterval = TimeSpan.FromMinutes(5),
                        PrepareSchemaIfNecessary = true
                    }));
            });
        }
        else
        {
            builder.Services.AddHangfire(config => config.UseInMemoryStorage());
        }

        builder.Services.AddHangfireServer();
    }

    private static void CreateHangfireDatabaseIfNotExists(string connectionString)
    {
        using var connection = new MySqlConnection(connectionString);
        using var command = connection.CreateCommand();

        connection.Open();
        command.CommandText = "CREATE DATABASE IF NOT EXISTS `hangfire`";
        command.ExecuteNonQuery();
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
                Timeout = TimeSpan.FromSeconds(30),
                TimeoutStatusCode = StatusCodes.Status408RequestTimeout,
                WriteTimeoutResponse = context =>
                {
                    if (!context.Response.HasStarted)
                    {
                        throw new TimeoutException();
                    }

                    return Task.CompletedTask;
                }
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
                .AddInterceptors(new SlowQueryLoggerInterceptor());

            optionsBuilder
                .UseMySQL(Configuration.DatabaseConnectionString);
        });
    }

    public static void AddSingletons(this WebApplicationBuilder builder)
    {
        // TODO: Multiple of these repositories can be scoped instead of singleton if they don't hold state
        builder.Services.AddSingleton<SessionRepository>();
        builder.Services.AddSingleton<ChatChannelRepository>();
        builder.Services.AddSingleton<RateLimitRepository>();
        builder.Services.AddSingleton<MatchRepository>();

        builder.Services.AddSingleton<SunriseMetrics>();

        builder.Services.AddSingleton(ConnectionMultiplexer.Connect($"{Configuration.RedisConnection},allowAdmin=true"));

    }



    public static void AddProblemDetails(this WebApplicationBuilder builder)
    {
        builder.Services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Instance =
                    $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
                context.ProblemDetails.Extensions.TryAdd("requestId", context.HttpContext.TraceIdentifier);

                var activity = context.HttpContext.Features.Get<IHttpActivityFeature>()?.Activity;
                context.ProblemDetails.Extensions.TryAdd("traceId", activity?.Id);
            };
        });

        builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
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
        builder.Services.AddScoped<UserService>();
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

        builder.Services.AddScoped<UserMetadataService>();

        builder.Services.AddScoped<UserRelationshipService>();

        builder.Services.AddScoped<UserInventoryItemService>();

        builder.Services.AddScoped<UserModerationService>();
        builder.Services.AddScoped<UserMedalsService>();
        builder.Services.AddScoped<UserFavouritesService>();
        builder.Services.AddScoped<UserFileService>();

        builder.Services.AddScoped<UserGradesService>();

        builder.Services.AddScoped<BeatmapHypeService>();
        builder.Services.AddScoped<CustomBeatmapStatusService>();

        builder.Services.AddScoped<EventRepository>();
        builder.Services.AddScoped<UserEventService>();
        builder.Services.AddScoped<BeatmapEventService>();

        builder.Services.AddScoped<ScoreRepository>();
        builder.Services.AddScoped<ScoreFileService>();
    }


    public static void AddServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<DirectService>();
        builder.Services.AddScoped<MedalService>();
        builder.Services.AddScoped<AssetBanchoService>();
        builder.Services.AddScoped<Services.AuthService>();
        builder.Services.AddScoped<BanchoService>();
        builder.Services.AddScoped<HttpClientService>();

        builder.Services.AddScoped<ScoreService>();
        builder.Services.AddScoped<UserBanchoService>();

        builder.Services.AddScoped<Services.AuthService>();

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
        app.Services.GetRequiredService<SunriseMetrics>();
    }

    public static void UseScalarApiReference(this WebApplication app)
    {
        app.UseSwagger(options => { options.RouteTemplate = "/openapi/{documentName}.json"; });

        app.MapScalarApiReference("docs",
            options =>
            {
                options.Title = "Sunrise API Documentation";
                options.Theme = ScalarTheme.Mars;

                options.WithModels(false);
                options.WithDownloadButton(false);

                options
                    .WithPreferredScheme("Bearer")
                    .AddHttpAuthentication("Bearer", auth => { auth.Token = "ey..."; });
            });
    }

    public static void ApplyDatabaseBootstrapping(this WebApplication app)
    {
        using var scope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        if (!Configuration.IsTestingEnv)
            database.DbContext.Database.Migrate();

        DatabaseSeeder.UseAsyncSeeding(database.DbContext).Wait();
    }

    public static void UseStaticBackgrounds(this WebApplication app)
    {
        if (Configuration.UseCustomBackgrounds)
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
        if (Configuration.UseW3CFileLogging)
            app.UseW3CLogging();

        if (Configuration.UseMetrics)
            app.UseOpenTelemetryPrometheusScrapingEndpoint();

        app.UseSerilogRequestLogging(opts =>
        {
            opts.MessageTemplate = "HTTP {RequestMethod} {Subdomain} {RequestPathWithQuery} responded {StatusCode} in {Elapsed:0.0000} ms";

            opts.EnrichDiagnosticContext = (diag, http) =>
            {
                diag.Set("TraceId", http.TraceIdentifier);
                diag.Set("RemoteIp", http.Connection.RemoteIpAddress?.ToString());

                var host = http.Request.Host.Host;
                var subdomain = host.Contains('.') ? host.Split('.')[0] : host;
                diag.Set("Subdomain", subdomain);

                diag.Set("RequestPathWithQuery",
                    http.Request.Path + (
                        http.Request.QueryString.HasValue ? http.Request.QueryString.Value : string.Empty)
                );
            };
        });

        app.UseRouting();

        app.UseAuthentication();

        app.UseMiddlewares();

        app.UseAuthorization();

        app.MapControllers();
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

public class GenerateAdditionalOpenApiSchema : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var schema = context.SchemaGenerator.GenerateSchema(typeof(CustomBeatmapStatusChangeResponse), context.SchemaRepository);

        swaggerDoc.Components.Schemas.TryAdd(nameof(CustomBeatmapStatusChangeResponse), schema);
    }
}

public class ProblemDetailsExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var status = exception switch
        {
            ArgumentException => StatusCodes.Status400BadRequest,
            BadHttpRequestException => StatusCodes.Status400BadRequest,
            AuthenticationException => StatusCodes.Status403Forbidden,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            TimeoutException => StatusCodes.Status408RequestTimeout,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status500InternalServerError
        };
        httpContext.Response.StatusCode = status;

        var title = ReasonPhrases.GetReasonPhrase(status);

        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = exception.Message,
            Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}"
        };

        problemDetails.Extensions.TryAdd("requestId", httpContext.TraceIdentifier);

        var activity = httpContext.Features.Get<IHttpActivityFeature>()?.Activity;
        problemDetails.Extensions.TryAdd("traceId", activity?.Id);

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}

internal class TraceEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current ?? null;
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", activity?.TraceId));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SpanId", activity?.SpanId));
    }
}