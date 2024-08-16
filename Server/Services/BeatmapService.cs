using Sunrise.Server.Data;
using Sunrise.Server.Helpers;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.Serializable;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Services;

public static class BeatmapService
{
    private const string Api = "https://osu.direct/api/";
    private const string BeatmapMirror = "https://old.ppy.sh/osu/";

    public static async Task<BeatmapSet?> GetBeatmapSet(Session session, int? beatmapSetId = null, string? beatmapHash = null, int? beatmapId = null)
    {
        if (beatmapSetId == null && beatmapHash == null && beatmapId == null)
        {
            return null;
        }

        var redis = ServicesProviderHolder.ServiceProvider.GetRequiredService<RedisRepository>();

        var beatmapSet = await redis.Get<BeatmapSet?>([string.Format(RedisKey.BeatmapSetBySetId, beatmapSetId), string.Format(RedisKey.BeatmapSetByHash, beatmapHash), string.Format(RedisKey.BeatmapSetByBeatmapId, beatmapId)]);

        if (beatmapSet != null)
        {
            return beatmapSet;
        }

        // TODO: Add beatmapSet in to DB with beatmaps.

        if (beatmapId != null) beatmapSet = await RequestsHelper.SendRequest<BeatmapSet>(session, ApiType.BeatmapSetDataByBeatmapId, [beatmapId]);
        if (beatmapHash != null && beatmapSet == null) beatmapSet = await RequestsHelper.SendRequest<BeatmapSet>(session, ApiType.BeatmapSetDataByHash, [beatmapHash]);
        if (beatmapSetId != null && beatmapSet == null) beatmapSet = await RequestsHelper.SendRequest<BeatmapSet>(session, ApiType.BeatmapSetDataById, [beatmapSetId]);

        if (beatmapSet == null)
        {
            return null;
        }

        if (Configuration.IgnoreBeatmapRanking)
        {
            foreach (var b in beatmapSet.Beatmaps)
            {
                b.StatusString = "ranked";
            }
        }

        foreach (var b in beatmapSet.Beatmaps)
        {
            await redis.Set([string.Format(RedisKey.BeatmapSetByBeatmapId, b.Id), string.Format(RedisKey.BeatmapSetByHash, b.Checksum)], beatmapSet);
        }

        await redis.Set(string.Format(RedisKey.BeatmapSetBySetId, beatmapSet.Id), beatmapSet);

        return beatmapSet;
    }

    public static async Task<byte[]?> GetBeatmapFile(Session session, int beatmapId)
    {
        var database = ServicesProviderHolder.ServiceProvider.GetRequiredService<SunriseDb>();
        var beatmapFile = await database.GetBeatmapFile(beatmapId);

        if (beatmapFile != null)
        {
            return beatmapFile;
        }

        beatmapFile = await RequestsHelper.SendRequest<byte[]>(session, ApiType.BeatmapDownload, [beatmapId]);

        if (beatmapFile == null)
        {
            return null;
        }

        await database.SetBeatmapFile(beatmapId, beatmapFile);
        return beatmapFile;
    }
}