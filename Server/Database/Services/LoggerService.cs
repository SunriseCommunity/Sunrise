using Sunrise.Server.Application;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Repositories;
using Watson.ORM.Sqlite;

namespace Sunrise.Server.Database.Services;

public class LoggerService
{
    private readonly WatsonORM _database;
    private readonly ILogger _logger;
    private readonly RedisRepository _redis;
    private readonly DatabaseManager _services;

    public LoggerService(DatabaseManager services,RedisRepository redis, WatsonORM database)
    {
        var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        _logger = loggerFactory.CreateLogger<LoggerService>();

        _services = services;

        _database = database;
        _redis = redis;
    }

    public async Task AddNewLoginEvent(int userId, string ip, string loginData)
    {
        var loginEvent = new LoginEvent
        {
            UserId = userId,
            Ip = ip,
            LoginData = loginData
        };

        await _database.InsertAsync(loginEvent);
    }
}