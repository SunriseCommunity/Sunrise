using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models.Events;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects.Serializable.Events;
using Sunrise.Shared.Utils;

namespace Sunrise.Shared.Database.Services.Events;

public class BeatmapEventService(SunriseDbContext dbContext)
{
    public async Task<Result> AddBeatmapSetHypeEvent(int userId, int beatmapSetId)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var beatmapEvent = new EventBeatmap
            {
                ExecutorId = userId,
                EventType = BeatmapEventType.BeatmapSetHyped,
                BeatmapSetId = beatmapSetId
            };

            dbContext.EventBeatmaps.Add(beatmapEvent);
            await dbContext.SaveChangesAsync();
        });
    }
    
    public async Task<Result> AddBeatmapSetHypeClearEvent(int userId, int beatmapSetId)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var beatmapEvent = new EventBeatmap
            {
                ExecutorId = userId,
                EventType = BeatmapEventType.BeatmapSetHypeCleared,
                BeatmapSetId = beatmapSetId
            };

            dbContext.EventBeatmaps.Add(beatmapEvent);
            await dbContext.SaveChangesAsync();
        });
    }

    public async Task<Result> AddBeatmapStatusChangedEvent(int userId, int beatmapSetId, string beatmapHash, BeatmapStatusWeb? newStatus)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var beatmapEvent = new EventBeatmap
            {
                ExecutorId = userId,
                EventType = BeatmapEventType.BeatmapStatusChanged,
                BeatmapSetId = beatmapSetId
            };

            beatmapEvent.SetData(new BeatmapStatusChanged
            {
                NewStatus = newStatus,
                BeatmapHash = beatmapHash
            });

            dbContext.EventBeatmaps.Add(beatmapEvent);
            await dbContext.SaveChangesAsync();
        });
    }

    public async Task<(List<EventBeatmap>, int)> GetBeatmapSetEvents(int? beatmapSetId = null, QueryOptions? options = null, CancellationToken ct = default)
    {
        var query = dbContext.EventBeatmaps
            .Where(b => !beatmapSetId.HasValue || b.BeatmapSetId == beatmapSetId)
            .OrderByDescending(x => x.Id);

        var totalCount = options?.IgnoreCountQueryIfExists == true ? -1 : await query.CountAsync(cancellationToken: ct);

        var events = await query.UseQueryOptions(options).ToListAsync(cancellationToken: ct);

        return (events, totalCount);
    }
}