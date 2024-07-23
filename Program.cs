using Microsoft.Extensions.FileProviders;
using Sunrise.Database.Sqlite;
using Sunrise.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<PlayerRepository>();
builder.Services.AddSingleton<SqliteDatabase>();
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

app.Use(async (context, next) =>
{
    Console.WriteLine(context.Request.Path);
    await next(context);
});

app.Run();