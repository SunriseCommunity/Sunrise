using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.FileProviders;
using Sunrise.Server.Data;
using Sunrise.Server.Repositories;
using Sunrise.Server.Repositories.Chat;
using Sunrise.Server.Utils;

namespace Sunrise.Server;

public static class Bootstrap
{
    public static void Configure(this WebApplicationBuilder builder)
    {
        builder.Services.AddAuthorization();
        builder.Services.AddAuthentication();
        builder.Services.AddControllers();

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddProblemDetails();

        builder.Services.AddW3CLogging(logging =>
        {
            logging.LoggingFields = W3CLoggingFields.All;
            logging.AdditionalRequestHeaders.Add("x-forwarded-for");
            logging.AdditionalRequestHeaders.Add("osu-version");
            if (Configuration.IncludeUserTokenInLogs) logging.AdditionalRequestHeaders.Add("osu-token");
        });
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
        builder.Services.AddSingleton<SessionRepository>();
        builder.Services.AddSingleton<ChannelRepository>();

        builder.Services.AddSingleton<RedisRepository>();
        builder.Services.AddSingleton<SunriseDb>();
    }

    public static void UseStaticBackgrounds(this WebApplication app)
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "Data/Files/SeasonalBackgrounds")),
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
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseW3CLogging();

        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();
    }

    public static void Setup(this WebApplication app)
    {
        CommandRepository.GetHandlers();
        PacketRepository.GetHandlers();

        ServicesProviderHolder.ServiceProvider = app.Services;

        Configuration.InsertApiServersIfNotExists();
    }
}