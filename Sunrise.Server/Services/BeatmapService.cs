using Sunrise.Server.Enums;
using Sunrise.Server.Extensions;
using Sunrise.Server.Utils;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Helpers.Requests;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Objects.Session;
using Sunrise.Shared.Repositories;

namespace Sunrise.Server.Services;

public class BeatmapService
{
    public async Task<string> SearchBeatmap(Session session, int? setId, int? beatmapId, string? beatmapHash)
    {
        var beatmapSet =
            await BeatmapRepository.GetBeatmapSet(session, setId, beatmapId: beatmapId, beatmapHash: beatmapHash);

        return beatmapSet != null ? beatmapSet.ToSearchResult(session) : "0";
    }

    public async Task<string?> SearchBeatmapSet(
        Session session,
        int page,
        string query,
        string mode,
        int ranked)
    {
        var parsedStatus = BeatmapStatusSearchParser.WebStatusToSearchStatus(ranked);
        var beatmapStatus = parsedStatus == BeatmapStatusSearch.Any ? "" : parsedStatus.ToString("D");

        var beatmapSets = await SearchBeatmapSet(session, beatmapStatus, mode, page, query);

        if (beatmapSets == null)
            return "0";

        var result = new List<string>
        {
            beatmapSets.Count == 100 ? "101" : beatmapSets.Count.ToString()
        }.Concat(beatmapSets.Select(x => x.ToSearchResult(session))).ToList();

        return string.Join("\n", result);
    }

    private async Task<List<BeatmapSet>?> SearchBeatmapSet(Session session, string? rankedStatus, string mode,
        int page, string query)
    {
        var beatmapSets = await RequestsHelper.SendRequest<List<BeatmapSet>?>(session,
            ApiType.BeatmapSetSearch,
            [query, 100, page, rankedStatus, mode]);

        if (beatmapSets == null) return null;

        foreach (var set in beatmapSets)
        {
            set.UpdateBeatmapRanking();
        }

        return beatmapSets;
    }
}