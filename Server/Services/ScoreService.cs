using osu.Shared;
using Sunrise.Server.Application;
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

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        if (!SubmitScoreHelper.IsScoreValid(session,
                score,
                osuVersion,
                clientHash,
                beatmapHash,
                beatmap.Checksum,
                storyboardHash))
        {
            SubmitScoreHelper.ReportRejectionToMetrics(session, score, "Invalid checksums");
            await database.UserService.Moderation.RestrictPlayer(session.User.Id, -1, "Invalid checksums on score submission");
            return "error: no";
        }

        var scores = await database.ScoreService.GetBeatmapScores(score.BeatmapHash, score.GameMode);

        var userStats = await database.UserService.Stats.GetUserStats(score.UserId, score.GameMode);

        if (userStats == null)
        {
            SubmitScoreHelper.ReportRejectionToMetrics(session, score, "User stats not found");
            return "error: no";
        }

        var prevUserStats = userStats.Clone();
        var prevPBest = scores.GetPersonalBestOf(score.UserId);

        var prevUserRank = await database.UserService.Stats.GetUserRank(userStats.UserId, userStats.GameMode);
        prevUserStats.Rank = prevUserRank;

        var timeElapsed = SubmitScoreHelper.GetTimeElapsed(score, scoreTime, scoreFailTime);
        await userStats.UpdateWithScore(score, prevPBest, timeElapsed);

        if (replay is { Length: >= 24 })
        {
            var replayFile = await database.ScoreService.Files.UploadReplay(userStats.UserId, replay);
            score.ReplayFileId = replayFile.Id;
        }

        if (!SubmitScoreHelper.IsScoreFailed(score) && score.ReplayFileId == null)
        {
            SubmitScoreHelper.ReportRejectionToMetrics(session, score, "Replay file not found for passed score");
            return "error: no";
        }

        await database.ScoreService.InsertScore(score);
        await database.UserService.Stats.UpdateUserStats(userStats);


        if (SubmitScoreHelper.IsScoreFailed(score) || !score.IsRanked)
            return "error: no"; // No need to create chart for failed or unranked scores

        // Mods can change difficulty rating, important to recalculate it for right medal unlocking
        if ((int)score.GameMode != beatmap.ModeInt || (int)score.Mods > 0)
            beatmap.DifficultyRating = await Calculators
                .RecalcuteBeatmapDifficulty(session, score.BeatmapId, (int)score.GameMode, score.Mods);

        var newPBest = scores.GetNewPersonalScore(score);
        userStats.Rank = await database.UserService.Stats.GetUserRank(userStats.UserId, userStats.GameMode);

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
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var scores = await database.ScoreService.GetBeatmapScores(beatmapHash, mode, leaderboardType, mods, session.User);

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

        foreach (var score in leaderboardScores)
        {
            responses.Add(await score.GetString());
        }

        return string.Join("\n", responses);
    }
}