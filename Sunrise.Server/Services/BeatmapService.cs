using Sunrise.Server.Extensions;
using Sunrise.Server.Objects;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Helpers;
using Sunrise.Shared.Managers;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Types.Enums;
using Sunrise.Shared.Utils;

using ISession =  Sunrise.Shared.Types.Interfaces.ISession;

namespace Sunrise.Server.Services;

public static class BeatmapService
{
    public static async Task<string> SearchBeatmap(ISession session, int? setId, int? beatmapId, string? beatmapHash)
    {
        var beatmapSet =
            await BeatmapManager.GetBeatmapSet(session, setId, beatmapId: beatmapId, beatmapHash: beatmapHash);

        return beatmapSet != null ? beatmapSet.ToSearchResult(session) : "0";
    }

    public static async Task<string?> SearchBeatmapSet(
        ISession session,
        int page,
        string query,
        string mode,
        int ranked)
    {
        var parsedStatus = Parsers.WebStatusToSearchStatus(ranked);
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

    private static async Task<List<BeatmapSet>?> SearchBeatmapSet(ISession session, string? rankedStatus, string mode,
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