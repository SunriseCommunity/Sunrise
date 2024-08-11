using Microsoft.Extensions.FileProviders;
using Sunrise.Server.Data;
using Sunrise.Server.Repositories;
using Sunrise.Server.Repositories.Chat;
using Sunrise.Server.Utils;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthorization();
builder.Services.AddAuthentication();
builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<SessionRepository>();
builder.Services.AddSingleton<ChannelRepository>();

builder.Services.AddSingleton<RedisRepository>();
builder.Services.AddSingleton<SunriseDb>();

var app = builder.Build();

CommandRepository.GetHandlers();
PacketRepository.GetHandlers();

ServicesProviderHolder.ServiceProvider = app.Services;

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "Data/Files/SeasonalBackgrounds")),
    RequestPath = "/static"
});

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();