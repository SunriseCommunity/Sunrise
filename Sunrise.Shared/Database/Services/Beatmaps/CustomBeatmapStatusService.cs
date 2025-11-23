using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models.Beatmap;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Database.Repositories;
using Sunrise.Shared.Database.Services.Events;
using Sunrise.Shared.Enums.Beatmaps;

namespace Sunrise.Shared.Database.Services.Beatmaps;

public class CustomBeatmapStatusService(
    ILogger<ScoreRepository> logger,
    Lazy<DatabaseService> databaseService,
    SunriseDbContext dbContext,
    BeatmapHypeService beatmapHypeService,
    BeatmapEventService beatmapEventService)
{
    public async Task<CustomBeatmapStatus?> GetCustomBeatmapStatus(string beatmapHash, QueryOptions? options = null, CancellationToken ct = default)
    {
        return await dbContext.CustomBeatmapStatuses
            .Where(m => m.BeatmapHash == beatmapHash)
            .UseQueryOptions(options)
            .FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<List<CustomBeatmapStatus>> GetCustomBeatmapSetStatuses(int beatmapSetId, QueryOptions? options = null, CancellationToken ct = default)
    {
        return await dbContext.CustomBeatmapStatuses
            .Where(m => m.BeatmapSetId == beatmapSetId)
            .UseQueryOptions(options)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<(List<CustomBeatmapStatus>, int)> GetCustomBeatmapSetStatusesGroupBySetId(BeatmapStatusWeb[]? status = null, QueryOptions? options = null, CancellationToken ct = default)
    {
        var query = dbContext.CustomBeatmapStatuses
            .Where(cs => status == null || status.Length == 0 || status.Contains(cs.Status))
            .GroupBy(x => x.BeatmapSetId)
            .Select(g => g.First())
            .ToQueryString();

        var totalCount = options?.IgnoreCountQueryIfExists == true
            ? -1
            : await dbContext.CustomBeatmapStatuses
                .FromSqlRaw(query)
                .CountAsync(cancellationToken: ct);

        var customBeatmapStatuses = await dbContext.CustomBeatmapStatuses
            .FromSqlRaw(query)
            .OrderByDescending(x => x.Id)
            .UseQueryOptions(options)
            .ToListAsync(cancellationToken: ct);

        return (customBeatmapStatuses, totalCount);
    }

    public async Task<Result> AddCustomBeatmapStatus(CustomBeatmapStatus status)
    {
        return await databaseService.Value.CommitAsTransactionAsync(async () =>
        {
            var applyBeatmapHypesResult = await beatmapHypeService.ApplyBeatmapHypes(status.UpdatedByUserId, status.BeatmapSetId, status.Status);
            if (applyBeatmapHypesResult.IsFailure)
                throw new ApplicationException(applyBeatmapHypesResult.Error);

            var addBeatmapStatusChangedEventResult = await beatmapEventService.AddBeatmapStatusChangedEvent(status.UpdatedByUserId, status.BeatmapSetId, status.BeatmapHash, status.Status);
            if (addBeatmapStatusChangedEventResult.IsFailure)
                throw new ApplicationException(addBeatmapStatusChangedEventResult.Error);

            dbContext.CustomBeatmapStatuses.Add(status);
            await dbContext.SaveChangesAsync();
        });
    }

    public async Task<Result> UpdateCustomBeatmapStatus(CustomBeatmapStatus status)
    {
        return await databaseService.Value.CommitAsTransactionAsync(async () =>
        {
            var addBeatmapStatusChangedEventResult = await beatmapEventService.AddBeatmapStatusChangedEvent(status.UpdatedByUserId, status.BeatmapSetId, status.BeatmapHash, status.Status);
            if (addBeatmapStatusChangedEventResult.IsFailure)
                throw new ApplicationException(addBeatmapStatusChangedEventResult.Error);

            dbContext.UpdateEntity(status);
            await dbContext.SaveChangesAsync();
        });
    }

    public async Task<Result> DeleteCustomBeatmapStatus(CustomBeatmapStatus status)
    {
        return await databaseService.Value.CommitAsTransactionAsync(async () =>
        {
            var addBeatmapStatusChangedEventResult = await beatmapEventService.AddBeatmapStatusChangedEvent(status.UpdatedByUserId, status.BeatmapSetId, status.BeatmapHash, null);
            if (addBeatmapStatusChangedEventResult.IsFailure)
                throw new ApplicationException(addBeatmapStatusChangedEventResult.Error);

            dbContext.CustomBeatmapStatuses.Remove(status);
            await dbContext.SaveChangesAsync();
        });
    }
}