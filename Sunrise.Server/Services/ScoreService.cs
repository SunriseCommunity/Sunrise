using osu.Shared;
using Sunrise.Server.Extensions;
using Sunrise.Server.Services.Helpers.Scores;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Leaderboards;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Objects.Session;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Utils.Performance;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;

namespace Sunrise.Server.Services;

public class ScoreService
{
    public async Task<string> SubmitScore(Session session, string scoreSerialized, string beatmapHash,
        int scoreTime, int scoreFailTime, string osuVersion, string clientHash, IFormFile? replay,
        string? storyboardHash)
    {
        var beatmapSet = await BeatmapRepository.GetBeatmapSet(session, beatmapHash: beatmapHash);
        var beatmap = beatmapSet?.Beatmaps.FirstOrDefault(x => x.Checksum == beatmapHash);
        if (beatmap == null || beatmapSet == null)
            throw new Exception("Invalid request: BeatmapSet not found");

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var score = scoreSerialized.TryParseToSubmittedScore(session, beatmap);
        var dbScore = await database.ScoreService.GetScore(score.ScoreHash);

        if (dbScore != null)
        {
            SubmitScoreHelper.ReportRejectionToMetrics(session, scoreSerialized, "Score with same hash already exists");
            return "error: no";
        }

        if (SubmitScoreHelper.IsHasInvalidMods(score.Mods))
        {
            SubmitScoreHelper.ReportRejectionToMetrics(session, scoreSerialized, "Invalid mods");
            return "error: no";
        }

        var notStandardMods = score.Mods.TryGetSelectedNotStandardMods();

        var hasNonStandardMods = notStandardMods is not Mods.None;
        var isHasMoreThanOneNotStandardMod = !notStandardMods.IsSingleMod() && hasNonStandardMods;
        var isNonSupportedNonStandardMod = (int)score.GameMode < 4 && hasNonStandardMods;

        // Disallow submitting scores with double not standard mods (e.g. ScoreV2 + Relax) or with which we are not supporting (e.g. shouldn't exist)
        if (isHasMoreThanOneNotStandardMod || isNonSupportedNonStandardMod)
        {
            SubmitScoreHelper.ReportRejectionToMetrics(session, scoreSerialized, "Includes non-standard mod(s), which is not supported for this game mode");
            return "error: no";
        }

        // Auto-restrict players who submit scores with too many performance points on standard game modes
        if (score.PerformancePoints >= Configuration.BannablePpThreshold && !hasNonStandardMods && score.LocalProperties.IsRanked)
        {
            SubmitScoreHelper.ReportRejectionToMetrics(session, scoreSerialized, "Too many performance points. Cheating?");
            await database.UserService.Moderation.RestrictPlayer(session.User.Id, -1, "Auto-restricted for submitting impossible score");
            return "error: no";
        }

        var isScoreValid = SubmitScoreHelper.IsScoreValid(session,
            score,
            clientHash,
            beatmapHash,
            beatmap.Checksum,
            storyboardHash);

        if (!isScoreValid)
        {
            SubmitScoreHelper.ReportRejectionToMetrics(session, scoreSerialized, "Invalid checksums");
            await database.UserService.Moderation.RestrictPlayer(session.User.Id, -1, "Invalid checksums on score submission");
            return "error: no";
        }

        var databaseScores = await database.ScoreService.GetBeatmapScores(score.BeatmapHash, score.GameMode);

        var globalScores = databaseScores.EnrichWithLeaderboardPositions();
        var scoresWithSameMods = globalScores.FindAll(x => x.Mods == score.Mods).EnrichWithLeaderboardPositions();

        var userStats = await database.UserService.Stats.GetUserStats(score.UserId, score.GameMode);

        if (userStats == null)
        {
            SubmitScoreHelper.ReportRejectionToMetrics(session, scoreSerialized, "User stats not found");
            return "error: no";
        }

        var prevUserStats = userStats.Clone();
        var prevPBest = globalScores.GetPersonalBestOf(score.UserId);

        var prevUserRank = await database.UserService.Stats.GetUserRank(userStats.UserId, userStats.GameMode);
        prevUserStats.LocalProperties.Rank = prevUserRank;

        var timeElapsed = SubmitScoreHelper.GetTimeElapsed(score, scoreTime, scoreFailTime);
        await userStats.UpdateWithScore(score, prevPBest, timeElapsed);

        if (replay is { Length: >= 24 })
        {
            var replayFile = await database.ScoreService.Files.UploadReplay(userStats.UserId, replay);
            score.ReplayFileId = replayFile.Id;
        }

        var isCurrentScoreFailed = SubmitScoreHelper.IsScoreFailed(score);

        if (!isCurrentScoreFailed && score.ReplayFileId == null)
        {
            SubmitScoreHelper.ReportRejectionToMetrics(session, scoreSerialized, "Replay file not found for passed score");
            return "error: no";
        }

        var scoreWithSameHash = globalScores.FirstOrDefault(x => x.ScoreHash == score.ScoreHash);

        if (scoreWithSameHash != null)
        {
            SubmitScoreHelper.ReportRejectionToMetrics(session, scoreSerialized, "Score with same hash already exists");
            return "error: no";
        }

        var prevPBestWithSameMods = scoresWithSameMods.GetPersonalBestOf(score.UserId);
        score.UpdateSubmissionStatus(prevPBestWithSameMods);

        if (prevPBestWithSameMods != null && score.SubmissionStatus == SubmissionStatus.Best)
        {
            // Best score shouldn't be failed, but adding this check just in case
            prevPBestWithSameMods.SubmissionStatus = prevPBestWithSameMods.IsPassed ? SubmissionStatus.Submitted : SubmissionStatus.Failed;
            await database.ScoreService.UpdateScore(prevPBestWithSameMods);
        }

        await database.ScoreService.InsertScore(score);
        await database.UserService.Stats.UpdateUserStats(userStats);


        if (isCurrentScoreFailed || !score.IsScoreable)
            return "error: no"; // No need to create chart for failed or for scores that are not scoreable

        // Mods can change difficulty rating, important to recalculate it for right medal unlocking
        if ((int)score.GameMode != beatmap.ModeInt || (int)score.Mods > 0)
            beatmap.DifficultyRating = await Calculators
                .RecalcuteBeatmapDifficulty(session, score.BeatmapId, (int)score.GameMode, score.Mods);

        var updatedScores = globalScores.UpsertUserScoreToSortedScores(score);
        var newPBest = updatedScores.GetPersonalBestOf(score.UserId) ?? score;

        userStats.LocalProperties.Rank = await database.UserService.Stats.GetUserRank(userStats.UserId, userStats.GameMode);

        if (newPBest.LocalProperties.LeaderboardPosition == 1 && globalScores.Count > 0 && globalScores[0].UserId != score.UserId)
        {
            var channels = ServicesProviderHolder.GetRequiredService<ChatChannelRepository>();
            channels.GetChannel(session, "#announce")
                ?.SendToChannel(SubmitScoreHelper.GetNewFirstPlaceString(session, newPBest, beatmapSet, beatmap));
        }

        return await SubmitScoreHelper.GetScoreSubmitResponse(beatmap, userStats, prevUserStats, newPBest, prevPBest);
    }

    public async Task<string> GetBeatmapScores(Session session, int setId, GameMode gameMode, Mods mods,
        LeaderboardType leaderboardType, string beatmapHash, string filename)
    {
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        gameMode = gameMode.EnrichWithMods(mods);

        var databaseScores = await database.ScoreService.GetBeatmapScores(beatmapHash, gameMode, leaderboardType, mods, session.User);
        var scores = databaseScores.EnrichWithLeaderboardPositions();

        var beatmapSet = await BeatmapRepository.GetBeatmapSet(session, setId, beatmapHash);
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

        var leaderboardScores = scores.GetScoresGroupedByUsersBest().Take(50);

        foreach (var score in leaderboardScores)
        {
            responses.Add(await score.GetString());
        }

        return string.Join("\n", responses);
    }
}