using osu.Shared;
using Sunrise.Server.Data;
using Sunrise.Server.Helpers;
using Sunrise.Server.Managers;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.Serializable;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Services;

public static class BeatmapService
{
    public static async Task<string> SearchBeatmapByIds(Session session, int setId, int beatmapId)
    {
        var beatmapSet = await BeatmapManager.GetBeatmapSet(session, setId, beatmapId: beatmapId);

        return beatmapSet != null ? beatmapSet.ToSearchResult(session) : "0";
    }

    public static async Task<string?> SearchBeatmapSet(
        Session session,
        int page,
        string query,
        string mode,
        int ranked)
    {
        var searchMostPlayed = query.Length >= 11 && query[..11] == "Most Played";
        query = query.Length switch
        {
            >= 6 when query[..6] == "Newest" => query[6..],
            >= 9 when query[..9] == "Top Rated" => query[9..],
            >= 11 when query[..11] == "Most Played" => query[11..],
            _ => query
        };

        var parsedStatus = Parsers.WebStatusToSearchStatus(ranked);
        var beatmapStatus = parsedStatus == BeatmapStatusSearch.Any ? "" : parsedStatus.ToString("D");

        List<BeatmapSet>? beatmapSets;

        if (searchMostPlayed)
        {
            var database = ServicesProviderHolder.ServiceProvider.GetRequiredService<SunriseDb>();
            var ids = await database.GetMostPlayedBeatmapsIds(Enum.TryParse<GameMode>(mode, out var modeEnum) ? modeEnum : null, page);

            beatmapSets = await BeatmapManager.SearchBeatmapsByIds(session, ids.Take(50).ToList());
            beatmapSets?.AddRange(await BeatmapManager.SearchBeatmapsByIds(session, ids.Skip(50).Take(50).ToList()) ?? []); // Not the best, but API ignores my page size parameter.
        }
        else
        {
            beatmapSets = await SearchBeatmapSet(session, beatmapStatus, mode, page, query);
        }

        if (beatmapSets == null)
            return "0";

        var result = new List<string>
        {
            beatmapSets.Count == 100 ? "101" : beatmapSets.Count.ToString()
        }.Concat(beatmapSets.Select(x => x.ToSearchResult(session))).ToList();

        return string.Join("\n", result);
    }

    private static async Task<List<BeatmapSet>?> SearchBeatmapSet(Session session, string? rankedStatus, string mode, int page, string query)
    {
        var beatmapSets = await RequestsHelper.SendRequest<List<BeatmapSet>?>(session, ApiType.BeatmapSetSearch, [query, 100, page, rankedStatus, mode]);

        if (beatmapSets == null)
        {
            return null;
        }

        // TODO: Save beatmapSets to DB with beatmaps and add redis logic.

        if (Configuration.IgnoreBeatmapRanking)
        {
            foreach (var b in beatmapSets)
            {
                b.StatusString = "ranked";

                foreach (var beatmap in b.Beatmaps)
                {
                    beatmap.StatusString = "ranked";
                }
            }
        }

        return beatmapSets;
    }
}