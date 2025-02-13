using ExpressionTree;
using Sunrise.Server.Application;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;
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

    public async Task<List<int>> GetRestrictedUsersIds()
    {
        var exp = new Expr("Id", OperatorEnum.IsNotNull, null);
        var restrictions = await _database.SelectManyAsync<Restriction>(exp);

        return restrictions.Select(x => x.UserId).ToList();
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

        user.AccountStatus = UserAccountStatus.Active;
        await _services.UserService.UpdateUser(user);
        await RefreshUserStats(user.Id);
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

        user.AccountStatus = UserAccountStatus.Restricted;
        await _services.UserService.UpdateUser(user);
        await _database.InsertAsync(restriction);
        await RefreshUserStats(user.Id);

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        var session = sessions.GetSession(userId: userId);
        session?.SendRestriction(reason);
    }

    public async Task<bool> EnableUser(int userId)
    {
        var user = await _services.UserService.GetUser(userId);
        if (user == null) return false;

        user.AccountStatus = UserAccountStatus.Active;

        await _services.UserService.UpdateUser(user);
        await RefreshUserStats(user.Id);

        return true;
    }

    public async Task<bool> DisableUser(int userId)
    {
        var user = await _services.UserService.GetUser(userId);
        if (user == null) return false;

        if (user.Username == Configuration.BotUsername)
        {
            _logger.LogWarning($"User {user.Username} is bot. Disabling bot user is not allowed.");
            return false;
        }

        user.AccountStatus = UserAccountStatus.Disabled;
        await _services.UserService.UpdateUser(user);
        await RefreshUserStats(user.Id);

        return true;
    }

    private async Task RefreshUserStats(int userId)
    {
        foreach (var i in Enum.GetValues(typeof(GameMode)))
        {
            var stat = await _services.UserService.Stats.GetUserStats(userId, (GameMode)i);
            if (stat == null)
                continue;

            var pp = await Calculators.CalculateUserWeightedPerformance(userId, (GameMode)i);
            var acc = await Calculators.CalculateUserWeightedAccuracy(userId, (GameMode)i);

            stat.PerformancePoints = pp;
            stat.Accuracy = acc;

            await _services.UserService.Stats.UpdateUserStats(stat);
        }
    }
}