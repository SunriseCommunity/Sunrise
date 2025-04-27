using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Users;
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

    public async Task<Result> UnrestrictPlayer(int userId)
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
            await RefreshUserStats(user.Id);

        });
    }

    public async Task<Result> RestrictPlayer(int userId, int? executorId, string reason, TimeSpan? expiresAfter = null)
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

            var user = await databaseService.Value.Users.GetUser(userId);
            if (user == null)
                return;

            if (user.Privilege >= UserPrivilege.Admin)
                return;

            user.AccountStatus = UserAccountStatus.Restricted;

            var updateUserResult = await databaseService.Value.Users.UpdateUser(user);
            if (updateUserResult.IsFailure)
                throw new ApplicationException(updateUserResult.Error);

            dbContext.Restrictions.Add(restriction);
            await dbContext.SaveChangesAsync();

            await RefreshUserStats(user.Id);

            var session = sessions.GetSession(userId: userId);
            session?.SendRestriction(reason);
        });
    }

    public async Task<Result> EnableUser(int userId)
    {
        return await databaseService.Value.CommitAsTransactionAsync(async () =>
        {
            var user = await databaseService.Value.Users.GetUser(userId);
            if (user == null) throw new ApplicationException(QueryResultError.REQUESTED_RECORD_NOT_FOUND);

            user.AccountStatus = UserAccountStatus.Active;

            var updateUserResult = await databaseService.Value.Users.UpdateUser(user);
            if (updateUserResult.IsFailure)
                throw new ApplicationException(updateUserResult.Error);

            await RefreshUserStats(user.Id);
        });
    }

    public async Task<Result> DisableUser(long userId)
    {
        return await databaseService.Value.CommitAsTransactionAsync(async () =>
        {
            var user = await databaseService.Value.Users.GetUser(userId);
            if (user == null) throw new ApplicationException(QueryResultError.REQUESTED_RECORD_NOT_FOUND);

            if (user.IsUserSunriseBot())
            {
                _logger.LogWarning($"User {user.Username} is bot. Disabling bot user is not allowed.");
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

            await RefreshUserStats(user.Id);
        });
    }

    private async Task RefreshUserStats(int userId)
    {
        foreach (var i in Enum.GetValues(typeof(GameMode)))
        {
            var stat = await databaseService.Value.Users.Stats.GetUserStats(userId, (GameMode)i);
            if (stat == null)
                continue;

            var pp = await calculatorService.CalculateUserWeightedPerformance(userId, (GameMode)i);
            var acc = await calculatorService.CalculateUserWeightedAccuracy(userId, (GameMode)i);

            stat.PerformancePoints = pp;
            stat.Accuracy = acc;

            var user = await dbContext.Users.FindAsync(userId);
            if (user == null) throw new Exception();

            await databaseService.Value.Users.Stats.UpdateUserStats(stat, user);
        }
    }
}