using Sunrise.Server.Data;
using Sunrise.Server.Helpers;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.Serializable;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Managers;

public class BeatmapManager
{
    public static async Task<BeatmapSet?> GetBeatmapSet(Session session, int? beatmapSetId = null, string? beatmapHash = null, int? beatmapId = null)
    {
        if (beatmapSetId == null && beatmapHash == null && beatmapId == null)
        {
            return null;
        }

        var redis = ServicesProviderHolder.ServiceProvider.GetRequiredService<RedisRepository>();

        var beatmapSet = await redis.Get<BeatmapSet?>([RedisKey.BeatmapSetBySetId(beatmapSetId ?? -1), RedisKey.BeatmapSetByHash(beatmapHash ?? ""), RedisKey.BeatmapSetByBeatmapId(beatmapId ?? -1)]);

        if (beatmapSet != null)
        {
            return beatmapSet;
        }

        // TODO: Add beatmapSet in to DB with beatmaps and move redis logic also to DB.

        if (beatmapId != null) beatmapSet = await RequestsHelper.SendRequest<BeatmapSet>(session, ApiType.BeatmapSetDataByBeatmapId, [beatmapId]);
        if (beatmapHash != null && beatmapSet == null) beatmapSet = await RequestsHelper.SendRequest<BeatmapSet>(session, ApiType.BeatmapSetDataByHash, [beatmapHash]);
        if (beatmapSetId != null && beatmapSet == null) beatmapSet = await RequestsHelper.SendRequest<BeatmapSet>(session, ApiType.BeatmapSetDataById, [beatmapSetId]);

        if (beatmapSet == null)
        {
            // TODO: Save null to cache if it requested map not found.
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
            await redis.Set([RedisKey.BeatmapSetByHash(b.Checksum), RedisKey.BeatmapSetByBeatmapId(b.Id)], beatmapSet);
        }

        await redis.Set(RedisKey.BeatmapSetBySetId(beatmapSet.Id), beatmapSet);

        return beatmapSet;
    }

    public static async Task<List<BeatmapSet>?> SearchBeatmapsByIds(Session session, List<int> ids)
    {
        var beatmapSets = await RequestsHelper.SendRequest<List<BeatmapSet>?>(session, ApiType.BeatmapsByBeatmapIds, [string.Join(",", ids.Select(x => x.ToString()))]);

        if (beatmapSets == null)
        {
            return null;
        }

        // TODO: Save beatmapSets to DB with beatmaps and add redis logic.

        if (Configuration.IgnoreBeatmapRanking)
            foreach (var b in beatmapSets)
            {
                b.StatusString = "ranked";

                foreach (var beatmap in b.Beatmaps)
                {
                    beatmap.StatusString = "ranked";
                }
            }

        return beatmapSets;
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