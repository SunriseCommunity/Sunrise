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
        string? beatmapHash = null, int? beatmapId = null, CancellationToken ct = default)
    {
        if (beatmapSetId == null && beatmapHash == null && beatmapId == null) return null;

        var beatmapSet = await database.Beatmaps.GetCachedBeatmapSet(beatmapSetId, beatmapHash, beatmapId);
        if (beatmapSet != null) return beatmapSet;

        if (beatmapId != null)
            beatmapSet =
                (await client.SendRequest<BeatmapSet>(session, ApiType.BeatmapSetDataByBeatmapId, [beatmapId], ct: ct)).GetValueOrDefault();
        if (beatmapHash != null && beatmapSet == null)
            beatmapSet =
                (await client.SendRequest<BeatmapSet>(session, ApiType.BeatmapSetDataByHash, [beatmapHash], ct: ct)).GetValueOrDefault();
        if (beatmapSetId != null && beatmapSet == null)
            beatmapSet =
                (await client.SendRequest<BeatmapSet>(session, ApiType.BeatmapSetDataById, [beatmapSetId], ct: ct)).GetValueOrDefault();

        if (beatmapSet == null)
            return null;

        await database.Beatmaps.SetCachedBeatmapSet(beatmapSet);

        var customStatuses = await database.CustomBeatmapStatuses.GetCustomBeatmapSetStatuses(beatmapSet.Id, ct: ct);

        beatmapSet.UpdateBeatmapRanking(customStatuses);

        return beatmapSet;
    }

    public async Task<List<BeatmapSet>?> SearchBeatmapSets(BaseSession session, string? rankedStatus, string mode,
        string query, Pagination pagination, CancellationToken ct = default)
    {
        var beatmapSets = (await client.SendRequest<List<BeatmapSet>?>(session,
            ApiType.BeatmapSetSearch,
            [query, pagination.PageSize, pagination.Page * pagination.PageSize, rankedStatus, mode],
            ct: ct)).GetValueOrDefault();

        if (beatmapSets == null) return null;


        foreach (var set in beatmapSets)
        {
            var customStatuses = await database.CustomBeatmapStatuses.GetCustomBeatmapSetStatuses(set.Id, ct: ct);

            set.UpdateBeatmapRanking(customStatuses);
        }

        return beatmapSets;
    }
}