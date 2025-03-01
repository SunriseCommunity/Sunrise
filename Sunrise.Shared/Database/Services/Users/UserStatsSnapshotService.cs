using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Utils;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Database.Services.Users;

public class UserStatsSnapshotService
{
    private readonly DatabaseService _databaseService;
    private readonly SunriseDbContext _dbContext;
    
    public UserStatsSnapshotService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
        _dbContext = databaseService.DbContext;
    }

    public async Task<UserStatsSnapshot> GetUserStatsSnapshot(int userId, GameMode mode)
    {
        var snapshot = await _dbContext.UserStatsSnapshot.Where(uss => uss.UserId == userId && uss.GameMode == mode).FirstOrDefaultAsync();

        if (snapshot == null)
        {
            snapshot = new UserStatsSnapshot
            {
                UserId = userId,
                GameMode = mode
            };
            await AddUserStatsSnapshot(snapshot);
        }

        return snapshot;
    }

    public async Task<Result> UpdateUserStatsSnapshot(UserStatsSnapshot snapshot)
    {
        return await ResultUtil.TryExecuteAsync(async () => { await _dbContext.SaveChangesAsync(); });
    }

    public async Task<Result> AddUserStatsSnapshot(UserStatsSnapshot snapshot)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            _dbContext.UserStatsSnapshot.Add(snapshot);
            await _dbContext.SaveChangesAsync();
        });
    }
}