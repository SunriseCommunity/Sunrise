using Microsoft.Extensions.FileProviders;
using Sunrise;
using Sunrise.Database;
using Sunrise.GameClient.Controllers;
using Sunrise.GameClient.Repositories;
using Sunrise.GameClient.Services;
using Sunrise.WebClient.Controllers;
using Sunrise.WebClient.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthorization();
builder.Services.AddAuthentication();
builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<SessionRepository>();
builder.Services.AddSingleton<Database>();

builder.Services.AddScoped<ServicesProvider>();
builder.Services.AddScoped<ScoreService>();
builder.Services.AddScoped<FileService>();
builder.Services.AddScoped<ApiService>();

builder.Services.AddScoped<BanchoController>();
builder.Services.AddScoped<AssetsController>();
builder.Services.AddScoped<WebController>();
builder.Services.AddScoped<ApiController>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "Database/Files/SeasonalBackgrounds")),
    RequestPath = "/static"
});

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();