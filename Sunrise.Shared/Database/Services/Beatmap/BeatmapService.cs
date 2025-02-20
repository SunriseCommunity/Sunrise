using Microsoft.Extensions.Logging;
using Sunrise.Shared.Database.Services.Beatmap.Services;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Objects.Keys;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Repositories;
using Watson.ORM.Sqlite;

namespace Sunrise.Shared.Database.Services.Beatmap;

public class BeatmapService
{
    private readonly WatsonORM _database;
    private readonly ILogger _logger;
    private readonly RedisRepository _redis;
    private readonly DatabaseManager _services;

    public BeatmapService(DatabaseManager services, RedisRepository redis, WatsonORM database)
    {
        var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        _logger = loggerFactory.CreateLogger<BeatmapService>();

        _database = database;
        _redis = redis;

        Files = new BeatmapFileService(_services, _redis, _database);
    }

    public BeatmapFileService Files { get; }

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
        var expiry = TimeSpan.FromHours(1); // TODO: Copy from observatory caching logic, but lower

        await _redis.Set(RedisKey.BeatmapSetBySetId(beatmapSet.Id), beatmapSet, expiry);

        foreach (var b in beatmapSet.Beatmaps)
        {
            await _redis.Set([RedisKey.BeatmapSetIdByHash(b.Checksum), RedisKey.BeatmapSetIdByBeatmapId(b.Id)],
                beatmapSet.Id,
                expiry);
        }
    }

    private async Task<BeatmapSet?> GetCachedBeatmapSetByBeatmapId(int beatmapId)
    {
        var beatmapSetId = await _redis.Get<int?>(RedisKey.BeatmapSetIdByBeatmapId(beatmapId));

        if (!beatmapSetId.HasValue) return null;

        return await GetCachedBeatmapSetBySetId(beatmapSetId.Value);
    }

    private async Task<BeatmapSet?> GetCachedBeatmapSetByBeatmapHash(string beatmapHash)
    {
        var beatmapSetId = await _redis.Get<int?>(RedisKey.BeatmapSetIdByHash(beatmapHash));

        if (!beatmapSetId.HasValue) return null;

        return await GetCachedBeatmapSetBySetId(beatmapSetId.Value);
    }

    private async Task<BeatmapSet?> GetCachedBeatmapSetBySetId(int beatmapSetId)
    {
        var beatmapSet = await _redis.Get<BeatmapSet?>(RedisKey.BeatmapSetBySetId(beatmapSetId));

        if (beatmapSet == null) return null;

        beatmapSet.UpdateBeatmapRanking();

        return beatmapSet;
    }
}