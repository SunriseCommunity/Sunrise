using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Helpers;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Shared.Services;

public class BeatmapService(DatabaseService database, HttpClientService client)
{
    public async Task<BeatmapSet?> GetBeatmapSet(BaseSession session, int? beatmapSetId = null,
        string? beatmapHash = null, int? beatmapId = null)
    {
        if (beatmapSetId == null && beatmapHash == null && beatmapId == null) return null;

        var beatmapSet = await database.Beatmaps.GetCachedBeatmapSet(beatmapSetId, beatmapHash, beatmapId);
        if (beatmapSet != null) return beatmapSet;

        if (beatmapId != null)
            beatmapSet =
                await client.SendRequest<BeatmapSet>(session, ApiType.BeatmapSetDataByBeatmapId, [beatmapId]);
        if (beatmapHash != null && beatmapSet == null)
            beatmapSet =
                await client.SendRequest<BeatmapSet>(session, ApiType.BeatmapSetDataByHash, [beatmapHash]);
        if (beatmapSetId != null && beatmapSet == null)
            beatmapSet =
                await client.SendRequest<BeatmapSet>(session, ApiType.BeatmapSetDataById, [beatmapSetId]);

        if (beatmapSet == null)
            return null;

        beatmapSet.UpdateBeatmapRanking();

        await database.Beatmaps.SetCachedBeatmapSet(beatmapSet);

        return beatmapSet;
    }

    public async Task<byte[]?> GetBeatmapFile(BaseSession session, int beatmapId)
    {
        var beatmapFile = await database.Beatmaps.Files.GetBeatmapFile(beatmapId);

        if (beatmapFile != null) return beatmapFile;

        beatmapFile = await client.SendRequest<byte[]>(session, ApiType.BeatmapDownload, [beatmapId]);

        if (beatmapFile == null) return null;

        await database.Beatmaps.Files.AddBeatmapFile(beatmapId, beatmapFile);
        return beatmapFile;
    }

    public async Task<List<BeatmapSet>?> SearchBeatmapSets(Session session, string? rankedStatus, string mode,
        string query, Pagination pagination)
    {
        var beatmapSets = await client.SendRequest<List<BeatmapSet>?>(session,
            ApiType.BeatmapSetSearch,
            [query, pagination.PageSize, pagination.Page * pagination.PageSize, rankedStatus, mode]);

        if (beatmapSets == null) return null;

        foreach (var set in beatmapSets)
        {
            set.UpdateBeatmapRanking();
        }

        return beatmapSets;
    }
}