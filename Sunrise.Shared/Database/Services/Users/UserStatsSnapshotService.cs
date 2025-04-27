using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Utils;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Database.Services.Users;

public class UserStatsSnapshotService(SunriseDbContext dbContext)
{
    public async Task<UserStatsSnapshot> GetUserStatsSnapshot(int userId, GameMode mode, CancellationToken ct = default)
    {
        var snapshot = await dbContext.UserStatsSnapshot.Where(uss => uss.UserId == userId && uss.GameMode == mode).FirstOrDefaultAsync(cancellationToken: ct);

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
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            dbContext.UpdateEntity(snapshot);
            await dbContext.SaveChangesAsync();
        });
    }

    public async Task<Result> AddUserStatsSnapshot(UserStatsSnapshot snapshot)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            dbContext.UserStatsSnapshot.Add(snapshot);
            await dbContext.SaveChangesAsync();
        });
    }
}