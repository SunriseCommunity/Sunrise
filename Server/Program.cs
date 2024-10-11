using Hangfire;
using Hangfire.Dashboard;
using Sunrise.Server;
using Sunrise.Server.Application;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

builder.AddSingletons();
builder.AddMiddlewares();
builder.AddHangfire();
builder.Configure();

var app = builder.Build();

app.UseHealthChecks("/health");

app.Setup();
app.UseStaticBackgrounds();
app.UseMiddlewares();
app.Configure();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = Array.Empty<IDashboardAuthorizationFilter>()
});

BackgroundTasks.Initialize();

app.Run();