using Microsoft.Extensions.FileProviders;
using Sunrise.Server.Controllers;
using Sunrise.Server.Data;
using Sunrise.Server.Repositories;
using Sunrise.Server.Services;
using Sunrise.Server.Utils;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthorization();
builder.Services.AddAuthentication();
builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<RedisRepository>();
builder.Services.AddSingleton<SessionRepository>();
builder.Services.AddSingleton<SunriseDb>();

builder.Services.AddScoped<ServicesProvider>();
builder.Services.AddScoped<ScoreService>();
builder.Services.AddScoped<FileService>();
builder.Services.AddScoped<BaseApiService>();

builder.Services.AddScoped<BanchoController>();
builder.Services.AddScoped<AssetsController>();
builder.Services.AddScoped<BeatmapService>();
builder.Services.AddScoped<WebController>();
builder.Services.AddScoped<BaseApiController>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "Data/Files/SeasonalBackgrounds")),
    RequestPath = "/static"
});

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();