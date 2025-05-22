using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models.Beatmap;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Database.Services.Beatmaps;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Objects.Keys;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Repositories;

namespace Sunrise.Shared.Database.Repositories;

public class BeatmapRepository(RedisRepository redis, CustomBeatmapStatusService customBeatmapStatusService, BeatmapHypeService beatmapHypeService)
{

    private readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(5);
    private readonly SemaphoreSlim _dbSemaphore = new(1);
    public CustomBeatmapStatusService CustomStatuses { get; } = customBeatmapStatusService;
    public BeatmapHypeService Hypes { get; } = beatmapHypeService;

    public async Task<BeatmapSet?> GetCachedBeatmapSet(int? beatmapSetId = null, string? beatmapHash = null, int? beatmapId = null)
    {
        if (beatmapSetId == null && beatmapHash == null && beatmapId == null) return null;

        if (beatmapId != null)
            return await GetCachedBeatmapSetByBeatmapId(beatmapId.Value);
        if (beatmapHash != null)
            return await GetCachedBeatmapSetByBeatmapHash(beatmapHash);
        if (beatmapSetId != null)
            return await GetCachedBeatmapSetBySetId(beatmapSetId.Value);

        return null;
    }

    public async Task SetCachedBeatmapSet(BeatmapSet beatmapSet)
    {
        await redis.Set(RedisKey.BeatmapSetBySetId(beatmapSet.Id), beatmapSet, _cacheTtl);

        foreach (var b in beatmapSet.Beatmaps)
        {
            await redis.Set([RedisKey.BeatmapSetIdByHash(b.Checksum), RedisKey.BeatmapSetIdByBeatmapId(b.Id)],
                beatmapSet.Id,
                _cacheTtl);
        }
    }

    private async Task<BeatmapSet?> GetCachedBeatmapSetByBeatmapId(int beatmapId)
    {
        var beatmapSetId = await redis.Get<int?>(RedisKey.BeatmapSetIdByBeatmapId(beatmapId));

        if (!beatmapSetId.HasValue) return null;

        return await GetCachedBeatmapSetBySetId(beatmapSetId.Value);
    }

    private async Task<BeatmapSet?> GetCachedBeatmapSetByBeatmapHash(string beatmapHash)
    {
        var beatmapSetId = await redis.Get<int?>(RedisKey.BeatmapSetIdByHash(beatmapHash));

        if (!beatmapSetId.HasValue) return null;

        return await GetCachedBeatmapSetBySetId(beatmapSetId.Value);
    }

    private async Task<BeatmapSet?> GetCachedBeatmapSetBySetId(int beatmapSetId, CancellationToken ct = default)
    {
        var beatmapSet = await redis.Get<BeatmapSet?>(RedisKey.BeatmapSetBySetId(beatmapSetId));

        if (beatmapSet == null) return null;

        try
        {
            await _dbSemaphore.WaitAsync(ct);

            var customStatuses = await CustomStatuses.GetCustomBeatmapSetStatuses(beatmapSet.Id,
                new QueryOptions(true)
                {
                    QueryModifier = q => q.Cast<CustomBeatmapStatus>().IncludeBeatmapNominator()
                },
                ct);

            beatmapSet.UpdateBeatmapRanking(customStatuses);
        }
        finally
        {
            _dbSemaphore.Release();
        }

        return beatmapSet;
    }
}