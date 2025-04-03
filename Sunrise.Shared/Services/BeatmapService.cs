using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Shared.Services;

public class BeatmapService(DatabaseService database, HttpClientService client)
{
    // TODO: Return Result
    public async Task<BeatmapSet?> GetBeatmapSet(BaseSession session, int? beatmapSetId = null,
        string? beatmapHash = null, int? beatmapId = null)
    {
        if (beatmapSetId == null && beatmapHash == null && beatmapId == null) return null;

        var beatmapSet = await database.Beatmaps.GetCachedBeatmapSet(beatmapSetId, beatmapHash, beatmapId);
        if (beatmapSet != null) return beatmapSet;

        if (beatmapId != null)
            beatmapSet =
                (await client.SendRequest<BeatmapSet>(session, ApiType.BeatmapSetDataByBeatmapId, [beatmapId])).GetValueOrDefault();
        if (beatmapHash != null && beatmapSet == null)
            beatmapSet =
                (await client.SendRequest<BeatmapSet>(session, ApiType.BeatmapSetDataByHash, [beatmapHash])).GetValueOrDefault();
        if (beatmapSetId != null && beatmapSet == null)
            beatmapSet =
                (await client.SendRequest<BeatmapSet>(session, ApiType.BeatmapSetDataById, [beatmapSetId])).GetValueOrDefault();

        if (beatmapSet == null)
            return null;

        var customStatuses = await database.CustomBeatmapStatuses.GetCustomBeatmapSetStatuses(beatmapSet.Id);

        beatmapSet.UpdateBeatmapRanking(customStatuses);

        await database.Beatmaps.SetCachedBeatmapSet(beatmapSet);

        return beatmapSet;
    }

    public async Task<List<BeatmapSet>?> SearchBeatmapSets(Session session, string? rankedStatus, string mode,
        string query, Pagination pagination)
    {
        var beatmapSets = (await client.SendRequest<List<BeatmapSet>?>(session,
            ApiType.BeatmapSetSearch,
            [query, pagination.PageSize, pagination.Page * pagination.PageSize, rankedStatus, mode])).GetValueOrDefault();

        if (beatmapSets == null) return null;


        foreach (var set in beatmapSets)
        {
            var customStatuses = await database.CustomBeatmapStatuses.GetCustomBeatmapSetStatuses(set.Id);

            set.UpdateBeatmapRanking(customStatuses);
        }

        return beatmapSets;
    }
}