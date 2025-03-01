using Sunrise.Server.Enums;
using Sunrise.Server.Extensions;
using Sunrise.Server.Utils;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Services;

namespace Sunrise.Server.Services;

public class DirectService(BeatmapService beatmapService)
{
    public async Task<string> SearchBeatmap(Session session, int? setId, int? beatmapId, string? beatmapHash)
    {
        var beatmapSet = await beatmapService.GetBeatmapSet(session, setId, beatmapId: beatmapId, beatmapHash: beatmapHash);

        return beatmapSet != null ? beatmapSet.ToSearchResult(session) : "0";
    }

    public async Task<string?> SearchBeatmapSets(
        Session session,
        int page,
        string query,
        string mode,
        int ranked)
    {
        var parsedStatus = BeatmapStatusSearchParser.WebStatusToSearchStatus(ranked);
        var beatmapStatus = parsedStatus == BeatmapStatusSearch.Any ? "" : parsedStatus.ToString("D");

        var beatmapSets = await beatmapService.SearchBeatmapSets(session, beatmapStatus, mode, query, new Pagination(page-1, 100));

        if (beatmapSets == null)
            return "0";

        var result = new List<string>
        {
            beatmapSets.Count == 100 ? "101" : beatmapSets.Count.ToString()
        }.Concat(beatmapSets.Select(x => x.ToSearchResult(session))).ToList();

        return string.Join("\n", result);
    }
}