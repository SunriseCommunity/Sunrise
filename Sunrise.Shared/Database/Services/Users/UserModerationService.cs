using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Services;

namespace Sunrise.Shared.Database.Services.Users;

public class UserModerationService(
    ILogger<UserModerationService> logger,
    Lazy<DatabaseService> databaseService,
    SessionRepository sessions,
    SunriseDbContext dbContext,
    CalculatorService calculatorService)
{
    private readonly ILogger _logger = logger;

    public async Task<string?> GetActiveRestrictionReason(int userId, CancellationToken ct = default)
    {
        var restrictionReason = await dbContext.Restrictions
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(r => r.Reason)
            .FirstOrDefaultAsync(cancellationToken: ct);

        return restrictionReason;
    }

    public async Task<bool> IsUserRestricted(int userId, CancellationToken ct = default)
    {
        var restrictionExpiryDate = await dbContext.Restrictions
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(r => r.ExpiryDate)
            .FirstOrDefaultAsync(cancellationToken: ct);

        if (restrictionExpiryDate == DateTime.MinValue)
            return false;

        if (restrictionExpiryDate >= DateTime.UtcNow)
            return true;

        await UnrestrictPlayer(userId);

        return false;
    }

    public async Task<Result> UnrestrictPlayer(int userId, int? executorId = null, string? executorIp = null)
    {
        return await databaseService.Value.CommitAsTransactionAsync(async () =>
        {
            var restrictions = await dbContext.Restrictions.Where(x => x.UserId == userId).ToListAsync();
            dbContext.RemoveRange(restrictions);

            await dbContext.SaveChangesAsync();

            var user = await databaseService.Value.Users.GetUser(userId);
            if (user == null)
                throw new ApplicationException(QueryResultError.REQUESTED_RECORD_NOT_FOUND);

            user.AccountStatus = UserAccountStatus.Active;
            await databaseService.Value.Users.UpdateUser(user);
            await RefreshUserStats(user);

            // TODO: Move outside, and also trigger if executed by server
            if (executorId.HasValue)
            {
                var executorUser = await databaseService.Value.Users.GetUser(executorId.Value);

                if (executorUser != null && !string.IsNullOrEmpty(executorIp))
                {
                    var userEventAction = new UserEventAction(executorUser, executorIp, userId, user);
                    var eventResult = await databaseService.Value.Events.Users.AddUserUnrestrictEvent(userEventAction);
                    if (eventResult.IsFailure)
                        throw new ApplicationException(eventResult.Error);
                }
            }
        });
    }

    public async Task<Result> RestrictPlayer(int userId, int? executorId, string reason, TimeSpan? expiresAfter = null, string? executorIp = null)
    {
        return await databaseService.Value.CommitAsTransactionAsync(async () =>
        {
            var restriction = new Restriction
            {
                UserId = userId,
                ExecutorId = executorId,
                Reason = reason,
                ExpiryDate = DateTime.UtcNow.Add(expiresAfter ?? TimeSpan.FromDays(365))
            };

            var user = await databaseService.Value.Users.GetUser(userId,
                options: new QueryOptions
                {
                    QueryModifier = q => q.Cast<User>().Include(u => u.UserStats)
                });
            if (user == null)
                throw new ApplicationException(QueryResultError.REQUESTED_RECORD_NOT_FOUND);

            if (user.Privilege >= UserPrivilege.Admin)
                throw new ApplicationException("Cannot restrict an admin or higher privileged user.");

            user.AccountStatus = UserAccountStatus.Restricted;

            var updateUserResult = await databaseService.Value.Users.UpdateUser(user);
            if (updateUserResult.IsFailure)
                throw new ApplicationException(updateUserResult.Error);

            dbContext.Restrictions.Add(restriction);
            await dbContext.SaveChangesAsync();

            await RefreshUserStats(user);

            var session = sessions.GetSession(userId: userId);
            session?.SendRestriction(reason);

            // TODO: Move outside
            if (executorId.HasValue)
            {
                var executorUser = await databaseService.Value.Users.GetUser(executorId.Value);

                if (executorUser != null)
                {
                    var ip = executorIp ?? "127.0.0.1";
                    var userEventAction = new UserEventAction(executorUser, ip, userId, user);
                    var eventResult = await databaseService.Value.Events.Users.AddUserRestrictEvent(userEventAction, reason, restriction.ExpiryDate);
                    if (eventResult.IsFailure)
                        throw new ApplicationException(eventResult.Error);
                }
            }
        });
    }

    public async Task<Result> EnableUser(int userId)
    {
        return await databaseService.Value.CommitAsTransactionAsync(async () =>
        {
            var user = await databaseService.Value.Users.GetUser(userId,
                options: new QueryOptions
                {
                    QueryModifier = q => q.Cast<User>().Include(u => u.UserStats)
                });
            if (user == null) throw new ApplicationException(QueryResultError.REQUESTED_RECORD_NOT_FOUND);

            user.AccountStatus = UserAccountStatus.Active;

            var updateUserResult = await databaseService.Value.Users.UpdateUser(user);
            if (updateUserResult.IsFailure)
                throw new ApplicationException(updateUserResult.Error);

            await RefreshUserStats(user);
        });
    }

    public async Task<Result> DisableUser(long userId)
    {
        return await databaseService.Value.CommitAsTransactionAsync(async () =>
        {
            var user = await databaseService.Value.Users.GetUser(userId,
                options: new QueryOptions
                {
                    QueryModifier = q => q.Cast<User>().Include(u => u.UserStats)
                });
            if (user == null) throw new ApplicationException(QueryResultError.REQUESTED_RECORD_NOT_FOUND);

            if (user.IsUserSunriseBot())
            {
                _logger.LogWarning("User {userUsername} is bot. Disabling bot user is not allowed.", user.Username);
                throw new ApplicationException(QueryResultError.CANT_UPDATE_REQUESTED_RECORD);
            }

            if (user.AccountStatus != UserAccountStatus.Active)
            {
                throw new ApplicationException("Cant set user's status to deactivated, since it will override the active restriction.");
            }

            user.AccountStatus = UserAccountStatus.Disabled;

            var updateUserResult = await databaseService.Value.Users.UpdateUser(user);
            if (updateUserResult.IsFailure)
                throw new ApplicationException(updateUserResult.Error);

            await RefreshUserStats(user);
        });
    }

    private async Task RefreshUserStats(User user)
    {
        foreach (var i in Enum.GetValues(typeof(GameMode)))
        {
            var mode = (GameMode)i;

            var stats = user.UserStats.FirstOrDefault(s => s.GameMode == mode);

            if (stats == null)
            {
                stats = await databaseService.Value.Users.Stats.GetUserStats(user.Id, mode);
                if (stats == null)
                    continue;
            }

            var pp = await calculatorService.CalculateUserWeightedPerformance(user, mode);
            var acc = await calculatorService.CalculateUserWeightedAccuracy(user, mode);

            stats.PerformancePoints = pp;
            stats.Accuracy = acc;

            await databaseService.Value.Users.Stats.UpdateUserStats(stats, user);
        }
    }
}