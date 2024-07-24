using Microsoft.Extensions.FileProviders;
using Sunrise;
using Sunrise.Database;
using Sunrise.Services;
using Sunrise.WebServer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<PlayersPoolService>();
builder.Services.AddSingleton<Database>();
builder.Services.AddSingleton<ServicesProvider>();
builder.Services.AddSingleton<ScoreService>();
builder.Services.AddSingleton<FileService>();
builder.Services.AddScoped<BanchoService>();

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