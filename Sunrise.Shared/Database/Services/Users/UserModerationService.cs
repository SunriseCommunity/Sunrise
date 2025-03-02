using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Services;

namespace Sunrise.Shared.Database.Services.Users;

public class UserModerationService
{
    private readonly CalculatorService _calculatorService;
    private readonly DatabaseService _databaseService;
    private readonly SunriseDbContext _dbContext;

    private readonly ILogger _logger;
    private readonly SessionRepository _sessions;

    public UserModerationService(DatabaseService databaseService, SessionRepository sessions, CalculatorService calculatorService)
    {
        var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        _logger = loggerFactory.CreateLogger<UserModerationService>();

        _databaseService = databaseService;
        _dbContext = databaseService.DbContext;
        _sessions = sessions;
        _calculatorService = calculatorService;
    }

    public async Task<string?> GetActiveRestrictionReason(int userId)
    {
        var restrictionReason = await _dbContext.Restrictions.AsNoTracking().Where(x => x.UserId == userId).Select(r => r.Reason).FirstOrDefaultAsync();

        return restrictionReason;
    }

    public async Task<bool> IsUserRestricted(int userId)
    {
        var restrictionExpiryDate = await _dbContext.Restrictions.AsNoTracking().Where(x => x.UserId == userId).Select(r => r.ExpiryDate).FirstOrDefaultAsync();

        if (restrictionExpiryDate == DateTime.MinValue)
            return false;

        if (restrictionExpiryDate >= DateTime.UtcNow)
            return true;

        await UnrestrictPlayer(userId);

        return false;
    }

    public async Task<Result> UnrestrictPlayer(int userId)
    {
        return await _databaseService.CommitAsTransactionAsync(async () =>
        {
            var restrictions = await _dbContext.Restrictions.Where(x => x.UserId == userId).ToListAsync();
            _dbContext.RemoveRange(restrictions);

            await _dbContext.SaveChangesAsync();

            var user = await _databaseService.Users.GetUser(userId);
            if (user == null)
                throw new ApplicationException(QueryResultError.REQUESTED_RECORD_NOT_FOUND);

            user.AccountStatus = UserAccountStatus.Active;
            await _databaseService.Users.UpdateUser(user);
            await RefreshUserStats(user.Id);

        });
    }

    public async Task<Result> RestrictPlayer(int userId, int? executorId, string reason, TimeSpan? expiresAfter = null)
    {
        return await _databaseService.CommitAsTransactionAsync(async () =>
        {
            var restriction = new Restriction
            {
                UserId = userId,
                ExecutorId = executorId,
                Reason = reason,
                ExpiryDate = DateTime.UtcNow.Add(expiresAfter ?? TimeSpan.FromDays(365))
            };

            var user = await _databaseService.Users.GetUser(userId);
            if (user == null)
                return;

            if (user.Privilege >= UserPrivilege.Admin)
                return;

            user.AccountStatus = UserAccountStatus.Restricted;

            var updateUserResult = await _databaseService.Users.UpdateUser(user);
            if (updateUserResult.IsFailure)
                throw new ApplicationException(updateUserResult.Error);

            _dbContext.Restrictions.Add(restriction);
            await _dbContext.SaveChangesAsync();

            await RefreshUserStats(user.Id);

            var session = _sessions.GetSession(userId: userId);
            session?.SendRestriction(reason);
        });
    }

    public async Task<Result> EnableUser(int userId)
    {
        return await _databaseService.CommitAsTransactionAsync(async () =>
        {
            var user = await _databaseService.Users.GetUser(userId);
            if (user == null) throw new ApplicationException(QueryResultError.REQUESTED_RECORD_NOT_FOUND);

            user.AccountStatus = UserAccountStatus.Active;

            var updateUserResult = await _databaseService.Users.UpdateUser(user);
            if (updateUserResult.IsFailure)
                throw new ApplicationException(updateUserResult.Error);

            await RefreshUserStats(user.Id);
        });
    }

    public async Task<Result> DisableUser(long userId)
    {
        return await _databaseService.CommitAsTransactionAsync(async () =>
        {
            var user = await _databaseService.Users.GetUser(userId);
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

            var updateUserResult = await _databaseService.Users.UpdateUser(user);
            if (updateUserResult.IsFailure)
                throw new ApplicationException(updateUserResult.Error);

            await RefreshUserStats(user.Id);
        });
    }

    private async Task RefreshUserStats(int userId)
    {
        foreach (var i in Enum.GetValues(typeof(GameMode)))
        {
            var stat = await _databaseService.Users.Stats.GetUserStats(userId, (GameMode)i);
            if (stat == null)
                continue;

            var pp = await _calculatorService.CalculateUserWeightedPerformance(userId, (GameMode)i);
            var acc = await _calculatorService.CalculateUserWeightedAccuracy(userId, (GameMode)i);

            stat.PerformancePoints = pp;
            stat.Accuracy = acc;

            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) throw new Exception();

            await _databaseService.Users.Stats.UpdateUserStats(stat, user);
        }
    }
}