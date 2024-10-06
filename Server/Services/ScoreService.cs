using osu.Shared;
using Sunrise.Server.Database;
using Sunrise.Server.Helpers;
using Sunrise.Server.Managers;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Services;

public static class ScoreService
{
    public static async Task<string> SubmitScore(Session session, string scoreSerialized, string beatmapHash,
        int scoreTime, int scoreFailTime, string osuVersion, string clientHash, IFormFile? replay,
        string? storyboardHash)
    {
        var beatmapSet = await BeatmapManager.GetBeatmapSet(session, beatmapHash: beatmapHash);
        var beatmap = beatmapSet?.Beatmaps.FirstOrDefault(x => x.Checksum == beatmapHash);
        if (beatmap == null || beatmapSet == null)
            throw new Exception("Invalid request: BeatmapFile not found");

        var score = scoreSerialized.TryParseToScore(beatmap, osuVersion);

        if (SubmitScoreHelper.IsHasInvalidMods(score.Mods))
        {
            SubmitScoreHelper.ReportRejectionToMetrics(session, score, "Invalid mods");
            return "error: no";
        }

        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();

        if (!SubmitScoreHelper.IsScoreValid(session, score, osuVersion, clientHash, beatmapHash, beatmap.Checksum,
                storyboardHash))
        {
            SubmitScoreHelper.ReportRejectionToMetrics(session, score, "Invalid checksums");
            await database.RestrictPlayer(session.User.Id, -1, "Invalid checksums on score submission");
            return "error: no";
        }

        var scores = await database.GetBeatmapScores(score.BeatmapHash, score.GameMode);

        var userStats = await database.GetUserStats(score.UserId, score.GameMode);

        if (userStats == null)
        {
            SubmitScoreHelper.ReportRejectionToMetrics(session, score, "User stats not found");
            return "error: no";
        }

        var prevUserStats = userStats.Clone();
        var prevPBest = scores.GetPersonalBestOf(score.UserId);

        var prevUserRank = await database.GetUserRank(userStats.UserId, userStats.GameMode);
        prevUserStats.Rank = prevUserRank;

        var timeElapsed = SubmitScoreHelper.GetTimeElapsed(score, scoreTime, scoreFailTime);
        await userStats.UpdateWithScore(score, prevPBest, timeElapsed);

        if (SubmitScoreHelper.IsScoreFailed(score))
        {
            await database.UpdateUserStats(userStats);
            return "error: no"; // Don't submit failed scores
        }

        if (replay is { Length: < 24 } or null)
        {
            SubmitScoreHelper.ReportRejectionToMetrics(session, score, "Replay file is too small");
            return "error: no";
        }

        var replayFile = await database.UploadReplay(userStats.UserId, replay);
        score.ReplayFileId = replayFile.Id;

        await database.InsertScore(score);
        await database.UpdateUserStats(userStats);

        var newPBest = scores.GetNewPersonalScore(score);
        userStats.Rank = await database.GetUserRank(userStats.UserId, userStats.GameMode);

        if (newPBest.LeaderboardRank == 1 && prevPBest?.LeaderboardRank != 1)
        {
            var channels = ServicesProviderHolder.GetRequiredService<ChannelRepository>();
            channels.GetChannel(session, "#announce")
                ?.SendToChannel(SubmitScoreHelper.GetNewFirstPlaceString(session, newPBest, beatmapSet, beatmap));
        }

        return await SubmitScoreHelper.GetScoreSubmitResponse(beatmap, userStats, prevUserStats, newPBest, prevPBest);
    }

    public static async Task<string> GetBeatmapScores(Session session, int setId, GameMode mode, Mods mods,
        LeaderboardType leaderboardType, string beatmapHash, string filename)
    {
        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();
        var scores = await database.GetBeatmapScores(beatmapHash, mode, leaderboardType, mods, session.User);

        var beatmapSet = await BeatmapManager.GetBeatmapSet(session, setId, beatmapHash);
        var beatmap = beatmapSet?.Beatmaps.FirstOrDefault(x => x.Checksum == beatmapHash);

        if (beatmapSet == null || beatmap == null)
            return $"{(int)BeatmapStatus.NotSubmitted}|false"; // TODO: Check with our db to find out if need's update

        if (beatmap.Status < BeatmapStatus.Ranked)
            return $"{(int)beatmap.Status}|false";

        var responses = new List<string>
        {
            $"{(int)beatmap.Status}|false|{beatmap.Id}|{beatmap.BeatmapsetId}|{scores.Count}",
            $"0\n{beatmapSet.Artist} - {beatmapSet.Title}\n10.0"
        };

        if (scores.Count == 0)
            return string.Join("\n", responses);

        var personalBest = scores.GetPersonalBestOf(session.User.Id);
        responses.Add(personalBest != null ? await personalBest.GetString() : "");

        var leaderboardScores = scores.GetTopScores(50);

        foreach (var score in leaderboardScores) responses.Add(await score.GetString());

        return string.Join("\n", responses);
    }
}