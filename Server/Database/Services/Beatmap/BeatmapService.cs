using Sunrise.Server.Database.Services.Beatmap.Services;
using Sunrise.Server.Repositories;
using Watson.ORM.Sqlite;

namespace Sunrise.Server.Database.Services.Beatmap;

public class BeatmapService
{
    private readonly WatsonORM _database;
    private readonly ILogger _logger;
    private readonly RedisRepository _redis;
    private readonly DatabaseManager _services;

    public BeatmapService(DatabaseManager services, RedisRepository redis, WatsonORM database)
    {
        var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        _logger = loggerFactory.CreateLogger<BeatmapService>();

        _database = database;
        _redis = redis;

        Files = new BeatmapFileService(_services, _redis, _database);
    }

    public BeatmapFileService Files { get; }

    // TODO: REDIS SAVING LOGIC HERE
}