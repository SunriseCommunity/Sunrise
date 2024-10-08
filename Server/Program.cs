using Hangfire;
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
app.UseHangfireDashboard();

app.Setup();
app.UseStaticBackgrounds();
app.UseMiddlewares();
app.Configure();

BackgroundTasks.Initialize();

app.Run();