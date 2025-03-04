using Hangfire;
using Sunrise.Server;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

builder.AddServices();
builder.AddSingletons();

builder.AddMiddlewares();
builder.AddApiEndpoints();

builder.AddSunriseDbContextPool();

builder.AddHangfire();
builder.Configure();

var app = builder.Build();

app.UseHangfireDashboard("/hangfire",
    new DashboardOptions
    {
        Authorization = [new HangfireAuthorizationFilter()]
    });

app.Setup();
app.UseHealthChecks("/health");

app.ApplyDatabaseMigrations();
app.UseStaticBackgrounds();
app.UseMiddlewares();
app.UseWebSockets();
app.Configure();

app.WarmUpSingletons();
BackgroundTasks.Initialize();

if (Configuration.ClearCacheOnStartup)
{
    using var scope = app.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();
    database.FlushAndUpdateRedisCache(false).Wait();
}

var sessions = app.Services.GetRequiredService<SessionRepository>();
sessions.AddBotToSession().Wait();

app.Run();

namespace Sunrise.Server
{
    public class Program;
}