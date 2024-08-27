using osu.Shared;
using Sunrise.Server.Data;
using Sunrise.Server.Helpers;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.Serializable;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Services;

public static class BeatmapService
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

    public static async Task<List<BeatmapSet>?> SearchBeatmapSet(Session session, string? rankedStatus, string mode, int page, string query)
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

    public static async Task<List<BeatmapSet>?> SearchBeatmapsByIds(Session session, List<int> ids)
    {
        var beatmapSets = await RequestsHelper.SendRequest<List<BeatmapSet>?>(session, ApiType.BeatmapsByBeatmapIds, [string.Join(",", ids.Select(x => x.ToString()))]);

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

    public static async Task<string?> SearchBeatmapSet(HttpRequest request)
    {
        var username = request.Query["u"];
        var passhash = request.Query["h"];
        var page = request.Query["p"];
        var query = Convert.ToString(request.Query["q"]) ?? "";
        var mode = Convert.ToString(request.Query["m"]) == "-1" ? "" : Convert.ToString(request.Query["m"]);
        var ranked = int.TryParse(request.Query["r"], out var rankedInt) ? rankedInt : -1;

        if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(page) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(passhash))
            return null;

        var sessions = ServicesProviderHolder.ServiceProvider.GetRequiredService<SessionRepository>();

        var session = sessions.GetSession(username: username);
        if (session == null)
            return null;

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
            var ids = await database.GetMostPlayedBeatmapsIds(Enum.TryParse<GameMode>(mode, out var modeEnum) ? modeEnum : null, int.TryParse(page, out var pageTemp) ? pageTemp : 1);

            beatmapSets = await SearchBeatmapsByIds(session, ids.Take(50).ToList());
            beatmapSets.AddRange(await SearchBeatmapsByIds(session, ids.Skip(50).Take(50).ToList())); // Not the best, but API ignores my page size parameter.
        }
        else
        {
            beatmapSets = await SearchBeatmapSet(session, beatmapStatus, mode, int.Parse(page!), query);
        }

        if (beatmapSets == null)
            return "0";

        var result = new List<string>
        {
            beatmapSets.Count == 100 ? "101" : beatmapSets.Count.ToString()
        }.Concat(beatmapSets.Select(x => x.ToSearchResult(session))).ToList();

        return string.Join("\n", result);
    }

    public static async Task<string?> SearchBeatmapSetByIds(HttpRequest request)
    {
        var username = request.Query["u"];
        var passhash = request.Query["h"];
        var setId = int.TryParse(request.Query["s"], out var setIdInt) ? setIdInt : -1;
        var beatmapId = int.TryParse(request.Query["b"], out var beatmapIdInt) ? beatmapIdInt : -1;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(passhash) || setId == -1 && beatmapId == -1)
            return null;

        var sessions = ServicesProviderHolder.ServiceProvider.GetRequiredService<SessionRepository>();

        var session = sessions.GetSession(username: username);
        if (session == null)
            return null;

        var beatmapSet = await GetBeatmapSet(session, setId == -1 ? null : setId, null, beatmapId == -1 ? null : beatmapId);

        return beatmapSet != null ? beatmapSet.ToSearchResult(session) : "0";
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