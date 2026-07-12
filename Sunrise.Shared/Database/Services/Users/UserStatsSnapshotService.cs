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
            var addResult = await AddUserStatsSnapshot(snapshot);
            if (addResult.IsFailure)
            {
                dbContext.Entry(snapshot).State = EntityState.Detached;
                snapshot = await dbContext.UserStatsSnapshot.FirstOrDefaultAsync(
                    uss => uss.UserId == userId && uss.GameMode == mode,
                    ct);

                if (snapshot == null)
                    throw new ApplicationException(addResult.Error);
            }
        }

        return snapshot;
    }

    public async Task<List<UserStatsSnapshot>> GetUserAllStatsSnapshot(int userId, CancellationToken ct = default)
    {
        var snapshots = await dbContext.UserStatsSnapshot.Where(uss => uss.UserId == userId).ToListAsync(ct);
        var existingModes = snapshots.Select(s => s.GameMode).ToHashSet();
        var missingModes = Enum.GetValues<GameMode>().Where(mode => !existingModes.Contains(mode)).ToList();

        var missingSnapshots = missingModes.Select(mode => new UserStatsSnapshot
        {
            UserId = userId,
            GameMode = mode
        }).ToList();

        if (missingSnapshots.Count != 0)
        {
            var addResult = await AddUserStatsSnapshot(missingSnapshots);
            if (addResult.IsSuccess)
            {
                snapshots.AddRange(missingSnapshots);
            }
            else
            {
                foreach (var missingSnapshot in missingSnapshots)
                    dbContext.Entry(missingSnapshot).State = EntityState.Detached;

                foreach (var missingMode in missingModes)
                    snapshots.Add(await GetUserStatsSnapshot(userId, missingMode, ct));
            }
        }

        return snapshots;
    }

    public async Task<Dictionary<int, UserStatsSnapshot>> GetUsersAllStatsSnapshot(List<int> userId, GameMode mode, CancellationToken ct = default)
    {
        var snapshots = await dbContext.UserStatsSnapshot.Where(uss => userId.Contains(uss.UserId) && uss.GameMode == mode).ToListAsync(ct);
        var result = snapshots.GroupBy(s => s.UserId).ToDictionary(g => g.Key, g => g.First());

        var missingUserIds = userId.Where(id => !result.ContainsKey(id)).ToList();

        var missingSnapshots = missingUserIds.Select(id => new UserStatsSnapshot
        {
            UserId = id,
            GameMode = mode
        }).ToList();

        if (missingSnapshots.Count == 0)
            return result;

        var addResult = await AddUserStatsSnapshot(missingSnapshots);
        if (addResult.IsSuccess)
        {
            foreach (var snapshot in missingSnapshots)
                result[snapshot.UserId] = snapshot;
        }
        else
        {
            foreach (var missingSnapshot in missingSnapshots)
                dbContext.Entry(missingSnapshot).State = EntityState.Detached;

            foreach (var missingUserId in missingUserIds)
            {
                var snapshot = await GetUserStatsSnapshot(missingUserId, mode, ct);
                result[snapshot.UserId] = snapshot;
            }
        }

        return result;
    }

    public async Task<Result> UpdateUserStatsSnapshot(UserStatsSnapshot snapshot)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            dbContext.UpdateEntity(snapshot);
            await dbContext.SaveChangesAsync();
        });
    }

    public async Task<Result> UpdateUserStatsSnapshot(List<UserStatsSnapshot> snapshot)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            dbContext.UpdateRange(snapshot);
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

    public async Task<Result> AddUserStatsSnapshot(List<UserStatsSnapshot> snapshot)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            dbContext.UserStatsSnapshot.AddRange(snapshot);
            await dbContext.SaveChangesAsync();
        });
    }
}
