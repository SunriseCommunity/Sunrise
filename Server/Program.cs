using Sunrise.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

builder.AddSingletons();
builder.AddMiddlewares();
builder.Configure();

var app = builder.Build();

app.UseHealthChecks("/health");

app.Setup();
app.UseStaticBackgrounds();
app.UseMiddlewares();
app.Configure();

app.Run();