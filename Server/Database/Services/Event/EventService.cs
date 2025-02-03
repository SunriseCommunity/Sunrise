using Sunrise.Server.Database.Services.Event.Services;
using Sunrise.Server.Repositories;
using Watson.ORM.Sqlite;

namespace Sunrise.Server.Database.Services.Event;

public class EventService
{
    private readonly WatsonORM _database;
    private readonly ILogger _logger;
    private readonly RedisRepository _redis;
    private readonly DatabaseManager _services;

    public EventService(DatabaseManager services, RedisRepository redis, WatsonORM database)
    {
        var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        _logger = loggerFactory.CreateLogger<EventService>();

        _services = services;

        _database = database;
        _redis = redis;

        UserEvent = new UserEventService(_services, _redis, _database);
    }

    public UserEventService UserEvent { get; }
}