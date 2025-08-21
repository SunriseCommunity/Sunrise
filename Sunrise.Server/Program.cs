using Hangfire;
using Sunrise.Server;
using Sunrise.Server.Middlewares;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

builder.AddServices();
builder.AddDatabaseServices();

builder.AddSingletons();

builder.AddMiddlewares();
builder.AddApiEndpoints();
builder.AddApiDocs();
builder.AddProblemDetails();

builder.AddAuthorizationPolicies();

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
app.UseScalarApiReference();

app.ApplyDatabaseBootstrapping();
app.UseStaticBackgrounds();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseWebSockets();
app.Configure();

app.WarmUpSingletons();
RecurringJobs.Initialize();

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