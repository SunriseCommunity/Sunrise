using Sunrise.Shared.Extensions;
using Sunrise.Shared.Objects.Keys;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Repositories;

namespace Sunrise.Shared.Database.Repositories;

public class BeatmapRepository(RedisRepository redis, CustomBeatmapStatusRepository customBeatmapStatusRepository)
{
    private readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(5);

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

    private async Task<BeatmapSet?> GetCachedBeatmapSetBySetId(int beatmapSetId)
    {
        var beatmapSet = await redis.Get<BeatmapSet?>(RedisKey.BeatmapSetBySetId(beatmapSetId));

        if (beatmapSet == null) return null;

        var customStatuses = await customBeatmapStatusRepository.GetCustomBeatmapSetStatuses(beatmapSet.Id);

        beatmapSet.UpdateBeatmapRanking(customStatuses);

        return beatmapSet;
    }
}