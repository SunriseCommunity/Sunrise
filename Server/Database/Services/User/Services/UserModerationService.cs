using ExpressionTree;
using Sunrise.Server.Application;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types.Enums;
using Watson.ORM.Sqlite;

namespace Sunrise.Server.Database.Services.User.Services;

public class UserModerationService
{
    private readonly WatsonORM _database;
    private readonly ILogger _logger;
    private readonly RedisRepository _redis;
    private readonly DatabaseManager _services;

    public UserModerationService(DatabaseManager services, RedisRepository redis, WatsonORM database)
    {
        var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        _logger = loggerFactory.CreateLogger<UserModerationService>();

        _services = services;
        _database = database;
        _redis = redis;
    }

    public async Task<string> GetRestrictionReason(int userId)
    {
        var exp = new Expr("UserId", OperatorEnum.Equals, userId);
        var restriction = await _database.SelectFirstAsync<Restriction?>(exp);
        return restriction?.Reason ?? string.Empty;
    }

    public async Task<bool> IsRestricted(int userId)
    {
        var exp = new Expr("UserId", OperatorEnum.Equals, userId);
        var restriction = await _database.SelectFirstAsync<Restriction?>(exp);
        if (restriction == null)
            return false;

        if (restriction.ExpiryDate >= DateTime.UtcNow)
            return true;

        await UnrestrictPlayer(userId, restriction);

        return false;
    }

    public async Task UnrestrictPlayer(int userId, Restriction? restriction = null)
    {
        var exp = new Expr("UserId", OperatorEnum.Equals, userId);

        restriction ??= await _database.SelectFirstAsync<Restriction?>(exp);

        if (restriction == null)
            return;

        await _database.DeleteAsync(restriction);

        var user = await _services.UserService.GetUser(userId);
        if (user == null)
            return;

        user.IsRestricted = false;
        await _services.UserService.UpdateUser(user);
    }

    public async Task RestrictPlayer(int userId, int executorId, string reason, TimeSpan? expiresAfter = null)
    {
        var restriction = new Restriction
        {
            UserId = userId,
            ExecutorId = executorId,
            Reason = reason,
            ExpiryDate = DateTime.UtcNow.Add(expiresAfter ?? TimeSpan.FromDays(365))
        };


        var user = await _services.UserService.GetUser(userId);
        if (user == null)
            return;

        if (user.Privilege >= UserPrivileges.Admin)
            return;

        user.IsRestricted = true;
        await _services.UserService.UpdateUser(user);
        await _database.InsertAsync(restriction);

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        var session = sessions.GetSession(userId: userId);
        session?.SendRestriction(reason);
    }
}