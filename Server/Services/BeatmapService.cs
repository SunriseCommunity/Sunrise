using Microsoft.AspNetCore.Mvc;
using Sunrise.Server.Data;
using Sunrise.Server.Helpers;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.Serializable;
using Sunrise.Server.Repositories;
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

    public static async Task<IActionResult> SearchBeatmapSet(HttpRequest request)
    {
        var username = request.Query["u"];
        var passhash = request.Query["h"];
        var ranked = int.TryParse(request.Query["r"], out var rankedInt) ? rankedInt : -1;
        var query = Convert.ToString(request.Query["q"]) ?? "";
        var mode = request.Query["m"];
        var page = request.Query["p"];

        if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(page) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(passhash))
            return new BadRequestObjectResult("Invalid request: Missing parameters");

        var database = ServicesProviderHolder.ServiceProvider.GetRequiredService<SunriseDb>();
        var user = await database.GetUser(username: username, passhash: passhash);

        if (user == null)
            return new BadRequestObjectResult("Invalid request: Invalid credentials");

        var sessions = ServicesProviderHolder.ServiceProvider.GetRequiredService<SessionRepository>();
        var session = sessions.GetSession(user.Id);

        if (session == null)
            return new BadRequestObjectResult("Invalid request: Invalid session");

        if (mode == "-1")
            mode = "";

        var rankedStatus = ranked switch
        {
            0 or 7 => BeatmapStatusSearch.Ranked,
            8 => BeatmapStatusSearch.Loved,
            3 => BeatmapStatusSearch.Qualified,
            2 => BeatmapStatusSearch.Pending,
            5 => BeatmapStatusSearch.Graveyard,
            _ => BeatmapStatusSearch.Any
        };

        query = query.Length switch
        {
            >= 6 when query[..6] == "Newest" => query[6..],
            >= 9 when query[..9] == "Top Rated" => query[9..],
            >= 11 when query[..11] == "Most Played" => query[11..], // TODO: Get our own most played maps from db
            _ => query
        };

        var rankedStatusString = ((int)rankedStatus).ToString() == "-2" ? "" : ((int)rankedStatus).ToString();

        var beatmapSets = await SearchBeatmapSet(session, rankedStatusString, mode!, int.Parse(page!), query);

        if (beatmapSets == null)
            return new OkObjectResult(0);

        List<string> result = [$"{(beatmapSets.Count == 100 ? 101 : beatmapSets.Count)}"];

        result.AddRange(beatmapSets.Select(x => x.ToSearchResult(session)));

        return new OkObjectResult(string.Join("\n", result));
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