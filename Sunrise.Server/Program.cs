using Hangfire;
using Sunrise.Server;
using Sunrise.Shared.Application;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

builder.AddServices();
builder.AddMiddlewares();

builder.AddHangfire();

builder.Configure();

var app = builder.Build();

app.UseHealthChecks("/health");


app.UseHangfireDashboard("/hangfire",
    new DashboardOptions
    {
        Authorization = [new HangfireAuthorizationFilter()]
    });

app.Setup();
app.UseStaticBackgrounds();
app.UseMiddlewares();
app.Configure();

app.WarmUpSingletons();

BackgroundTasks.Initialize();

app.Run();