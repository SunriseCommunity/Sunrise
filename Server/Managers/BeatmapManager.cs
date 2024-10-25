using Sunrise.Server.Application;
using Sunrise.Server.Database;
using Sunrise.Server.Helpers;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.Serializable;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types;
using Sunrise.Server.Types.Enums;

namespace Sunrise.Server.Managers;

public class BeatmapManager
{
    public static async Task<BeatmapSet?> GetBeatmapSet(BaseSession session, int? beatmapSetId = null,
        string? beatmapHash = null, int? beatmapId = null)
    {
        if (beatmapSetId == null && beatmapHash == null && beatmapId == null) return null;

        var redis = ServicesProviderHolder.GetRequiredService<RedisRepository>();

        var beatmapSet = await redis.Get<BeatmapSet?>([
            RedisKey.BeatmapSetBySetId(beatmapSetId ?? -1), RedisKey.BeatmapSetByHash(beatmapHash ?? ""),
            RedisKey.BeatmapSetByBeatmapId(beatmapId ?? -1)
        ]);

        if (beatmapSet != null) return beatmapSet;

        // TODO: Add beatmapSet in to DB with beatmaps and move redis logic also to DB.

        if (beatmapId != null)
            beatmapSet =
                await RequestsHelper.SendRequest<BeatmapSet>(session, ApiType.BeatmapSetDataByBeatmapId, [beatmapId]);
        if (beatmapHash != null && beatmapSet == null)
            beatmapSet =
                await RequestsHelper.SendRequest<BeatmapSet>(session, ApiType.BeatmapSetDataByHash, [beatmapHash]);
        if (beatmapSetId != null && beatmapSet == null)
            beatmapSet =
                await RequestsHelper.SendRequest<BeatmapSet>(session, ApiType.BeatmapSetDataById, [beatmapSetId]);

        if (beatmapSet == null)
            // TODO: Save null to cache if it requested map not found.
            return null;

        if (Configuration.IgnoreBeatmapRanking)
            foreach (var b in beatmapSet.Beatmaps)
            {
                b.StatusString = "ranked";
            }

        // NOTE: Redis cache Timespan is temporary solution until I'm working on proper beatmap mirror (other project).

        foreach (var b in beatmapSet.Beatmaps)
        {
            await redis.Set([RedisKey.BeatmapSetByHash(b.Checksum), RedisKey.BeatmapSetByBeatmapId(b.Id)],
                beatmapSet,
                TimeSpan.FromDays(30));
        }

        await redis.Set(RedisKey.BeatmapSetBySetId(beatmapSet.Id), beatmapSet, TimeSpan.FromDays(30));

        return beatmapSet;
    }

    public static async Task<List<BeatmapSet>?> SearchBeatmapsByIds(Session session, List<int> ids)
    {
        var beatmapSets = await RequestsHelper.SendRequest<List<BeatmapSet>?>(session,
            ApiType.BeatmapsByBeatmapIds,
            [string.Join(",", ids.Select(x => x.ToString()))]);

        if (beatmapSets == null) return null;

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

    public static async Task<byte[]?> GetBeatmapFile(BaseSession session, int beatmapId)
    {
        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();
        var beatmapFile = await database.GetBeatmapFile(beatmapId);

        if (beatmapFile != null) return beatmapFile;

        beatmapFile = await RequestsHelper.SendRequest<byte[]>(session, ApiType.BeatmapDownload, [beatmapId]);

        if (beatmapFile == null) return null;

        await database.SetBeatmapFile(beatmapId, beatmapFile);
        return beatmapFile;
    }
}