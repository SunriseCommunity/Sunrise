using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models.Beatmap;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Database.Repositories;
using Sunrise.Shared.Database.Services.Events;
using Sunrise.Shared.Database.Services.Users;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Utils;

namespace Sunrise.Shared.Database.Services.Beatmaps;

public class BeatmapHypeService(
    ILogger<ScoreRepository> logger,
    Lazy<DatabaseService> databaseService,
    SunriseDbContext dbContext,
    UserInventoryItemService userInventoryItemService,
    BeatmapEventService beatmapEventService
)
{
    public async Task<Result> AddBeatmapHypeFromUserInventory(User user, int beatmapSetId)
    {
        return await databaseService.Value.CommitAsTransactionAsync(async () =>
        {
            var beatmapsetCustomStatuses = await databaseService.Value.Beatmaps.CustomStatuses.GetCustomBeatmapSetStatuses(beatmapSetId);
            if (beatmapsetCustomStatuses.Any())
                throw new ApplicationException("You can't hype beatmapset with custom beatmap status");

            var inventoryHypes = await userInventoryItemService.GetInventoryItem(user.Id,
                ItemType.Hype,
                new QueryOptions(true)
                {
                    QueryModifier = q => q.Cast<UserInventoryItem>().Where(x => x.Quantity > 0)
                });

            if (inventoryHypes == null)
                throw new ApplicationException("Not enough hypes to add beatmap hype");

            var userBeatmapHype = await GetUserBeatmapHype(user, beatmapSetId);

            if (userBeatmapHype != null)
            {
                if (!Configuration.AllowMultipleHypeFromSameUser)
                {
                    throw new ApplicationException("You already hyped this beatmap set");
                }
                
                userBeatmapHype.Hypes += 1;
            }

            var hypeBeatmapResult = userBeatmapHype != null ? await UpdateBeatmapHype(userBeatmapHype) : await AddBeatmapHype(user, beatmapSetId);
            if (hypeBeatmapResult.IsFailure)
                throw new ApplicationException(hypeBeatmapResult.Error);

            inventoryHypes.Quantity -= 1;

            await userInventoryItemService.UpdateInventoryItem(inventoryHypes);

            var addBeatmapSetHypeEventResult = await beatmapEventService.AddBeatmapSetHypeEvent(user.Id, beatmapSetId);
            if (addBeatmapSetHypeEventResult.IsFailure)
                throw new ApplicationException(addBeatmapSetHypeEventResult.Error);
        });
    }

    public async Task<Result> ApplyBeatmapHypes(int userId, int beatmapSetId, BeatmapStatusWeb newBeatmapStatus)
    {
        return await databaseService.Value.CommitAsTransactionAsync(async () =>
        {
            // TODO: Send notifications to users about their hyped map status update

            var beatmapHypes = await GetBeatmapHypeCount(beatmapSetId);
            if (beatmapHypes <= 0)
                return;

            var addBeatmapSetHypeClearEventResult = await beatmapEventService.AddBeatmapSetHypeClearEvent(userId, beatmapSetId);
            if (addBeatmapSetHypeClearEventResult.IsFailure)
                throw new ApplicationException(addBeatmapSetHypeClearEventResult.Error);

            var removeBeatmapHypesResult = await RemoveBeatmapHypes(beatmapSetId);
            if (removeBeatmapHypesResult.IsFailure)
                throw new ApplicationException(removeBeatmapHypesResult.Error);
        });
    }

    public async Task<int> GetBeatmapHypeCount(int beatmapSetId)
    {
        return await dbContext.BeatmapHypes.Where(x => x.BeatmapSetId == beatmapSetId).SumAsync(x => x.Hypes);
    }

    public async Task<(List<(int, int)>, int)> GetHypedBeatmaps(QueryOptions? options = null, CancellationToken ct = default)
    {
        var hypesQuery = dbContext.BeatmapHypes.GroupBy(x => x.BeatmapSetId)
            .Select(g => new
            {
                g.Key,
                SUM = g.Sum(s => s.Hypes),
                LastHypedId = g.Max(s => s.Id)
            })
            .Where(g => g.SUM >= Configuration.HypesToStartHypeTrain)
            .OrderBy(g => g.SUM)
            .ThenByDescending(g => g.LastHypedId);

        var totalCount = options?.IgnoreCountQueryIfExists == true ? -1 : await hypesQuery.CountAsync(cancellationToken: ct);

        var hypes = await hypesQuery
            .UseQueryOptions(options)
            .ToListAsync(cancellationToken: ct);

        return (hypes.Select(g => (g.Key, g.SUM)).ToList(), totalCount);
    }

    private async Task<Result> AddBeatmapHype(User user, int beatmapSetId)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            dbContext.BeatmapHypes.Add(new BeatmapHype
            {
                UserId = user.Id,
                BeatmapSetId = beatmapSetId,
                Hypes = 1
            });
            await dbContext.SaveChangesAsync();
        });
    }

    private async Task<BeatmapHype?> GetUserBeatmapHype(User user, int beatmapSetId)
    {
        return await dbContext.BeatmapHypes.FirstOrDefaultAsync(x => x.UserId == user.Id && x.BeatmapSetId == beatmapSetId);
    }

    private async Task<Result> UpdateBeatmapHype(BeatmapHype hype)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            dbContext.UpdateEntity(hype);
            await dbContext.SaveChangesAsync();
        });
    }

    private async Task<Result> RemoveBeatmapHypes(int beatmapSetId)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            dbContext.BeatmapHypes.RemoveRange(dbContext.BeatmapHypes.Where(x => x.BeatmapSetId == beatmapSetId));
            await dbContext.SaveChangesAsync();
        });
    }
}