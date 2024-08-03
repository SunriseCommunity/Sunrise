using osu.Shared;
using Sunrise.Server.Helpers;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.Models;
using Sunrise.Server.Objects.Serializable;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Services;

public class ScoreService(ServicesProvider services)
{

    public async Task<string> SubmitScore(HttpRequest request)
    {

        var data = new SubmitScoreRequest(request);

        data.ThrowIfHasEmptyFields();

        var beatmap = await new BeatmapService(services).GetBeatmap(data.BeatmapHash!);

        if (beatmap == null)
        {
            throw new Exception("Invalid request: BeatmapFile not found");
        }

        var decryptedScore = Parsers.ParseSubmittedScore(data);

        var score = await new Score().SetScoreFromString(decryptedScore, services, beatmap, data.OsuVersion ?? "");

        ThrowIfInvalidMods(score.Mods);

        var rawScores = await services.Database.GetBeatmapScores(score.BeatmapHash, score.GameMode);
        var scores = new ScoresHelper(rawScores);

        var userStats = await services.Database.GetUserStats(score.UserId, score.GameMode);

        if (userStats == null)
        {
            throw new Exception("Invalid request: UserStats not found");
        }

        var prevUserStats = userStats.Clone();
        var prevPBest = scores.GetPersonalBestOf(score.UserId);

        var prevUserRank = await services.Database.GetUserRank(userStats.UserId, userStats.GameMode);
        prevUserStats.Rank = prevUserRank;

        var timeElapsed = GetTimeElapsed(score, data);
        await userStats.UpdateWithScore(score, prevPBest, timeElapsed, services);

        if (IsScoreFailed(score))
        {
            await services.Database.UpdateUserStats(userStats);
            return "error: no"; // Don't submit failed scores
        }

        var replayFile = await services.Database.UploadReplay(userStats.UserId, data.Replay!);
        score.ReplayFileId = replayFile.Id;

        await services.Database.InsertScore(score);
        await services.Database.UpdateUserStats(userStats);

        var newPBest = scores.GetNewPersonalScore(score);
        userStats.Rank = await services.Database.GetUserRank(userStats.UserId, userStats.GameMode);

        return GetScoreSubmitResponse(beatmap, userStats, prevUserStats, newPBest, prevPBest);
    }

    public async Task<string> GetBeatmapScores(HttpRequest request)
    {
        var data = new GetScoresRequest(request);

        data.ThrowIfHasEmptyFields();

        var rawScores = await services.Database.GetBeatmapScores(data.Hash!, data.Mode);
        var scores = new ScoresHelper(rawScores);

        var beatmap = await new BeatmapService(services).GetBeatmap(data.Hash!);

        if (beatmap == null)
        {
            return $"{(int)BeatmapStatus.NotSubmitted}|false";
        }

        if (beatmap.Status < BeatmapStatus.Ranked)
        {
            return $"{(int)beatmap.Status}|false";
        }

        // TODO: Handle if needs update 

        var responses = new List<string>
        {
            $"{(int)beatmap.Status}|false|{beatmap.Id}|{beatmap.BeatmapsetId}|{scores.Count}",
            $"0\n{data.BeatmapName?.Replace(".osu", "")}\n10.0"
        };

        var user = await services.Database.GetUser(username: data.Username);

        if (user == null || scores.Count == 0)
        {
            return string.Join("\n", responses);
        }

        var personalBest = scores.GetPersonalBestOf(user.Id);
        responses.Add(personalBest != null ? await personalBest.GetString(services) : "");

        var leaderboardScores = scores.GetTopScores(50);

        foreach (var score in leaderboardScores)
        {
            responses.Add(await score.GetString(services));
        }

        return string.Join("\n", responses);
    }

    private string GetScoreSubmitResponse(Beatmap beatmap, UserStats user, UserStats prevUser, Score newScore, Score? prevScore)
    {
        var userUrl = $"https://{Configuration.Domain}/user/{user.Id}";

        var beatmapInfo = $"beatmapId:{beatmap.Id}|beatmapSetId:{beatmap.BeatmapsetId}|beatmapPlaycount:{beatmap.Playcount}|beatmapPasscount:{beatmap.Passcount}|approvedDate:{beatmap.LastUpdated:yyyy-MM-dd}";
        var beatmapRanking = $"chartId:beatmap|chartUrl:{beatmap.Url}|chartName:Beatmap Ranking";
        var scoreInfo = string.Join("|", GetChart(prevScore, newScore));
        var playerInfo = $"chartId:overall|chartUrl:{userUrl}|chartName:Overall Ranking|" + string.Join("|", GetChart(prevUser, user));

        return $"{beatmapInfo}\n{beatmapRanking}|{scoreInfo}|onlineScoreId:{newScore.Id}\n{playerInfo}|achievements-new:";
    }

    private static List<string> GetChart<T>(T before, T after)
    {
        string[] chartEntries =
        [
            "Rank",
            "RankedScore",
            "TotalScore",
            "MaxCombo",
            "Accuracy",
            "Pp"
        ];

        var result = new List<string>();

        foreach (var entry in chartEntries)
        {
            var lowerFirst = char.ToLower(entry[0]) + entry.Substring(1);

            var obj = entry switch
            {
                "RankedScore" => typeof(T) == typeof(Score) ? "TotalScore" : "RankedScore",
                "Rank" => typeof(T) == typeof(Score) ? "LeaderboardRank" : "Rank",
                "Pp" => "PerformancePoints",
                _ => entry
            };

            result.Add(GetChartEntry(lowerFirst, before?.GetType().GetProperty(obj)?.GetValue(before), after?.GetType().GetProperty(obj)?.GetValue(after)));
        }

        return result;
    }

    private static string GetChartEntry(string name, object? before, object? after)
    {
        return $"{name}Before:{before?.ToString() ?? string.Empty}|{name}After:{after?.ToString() ?? string.Empty}";
    }

    private void ThrowIfInvalidMods(Mods mods)
    {
        if (mods.HasFlag(Mods.Relax) || mods.HasFlag(Mods.Autoplay) || mods.HasFlag(Mods.Target) ||
            mods.HasFlag(Mods.ScoreV2))
        {
            throw new Exception("Invalid request: Invalid mods");
        }
    }

    private int GetTimeElapsed(Score score, SubmitScoreRequest data)
    {
        var isPassed = score.IsPassed || score.Mods.HasFlag(Mods.NoFail);

        if (string.IsNullOrEmpty(data.ScoreTime) || string.IsNullOrEmpty(data.ScoreFailTime))
        {
            return 0;
        }

        return isPassed ? int.Parse(data.ScoreTime) : int.Parse(data.ScoreFailTime);
    }

    private bool IsScoreFailed(Score score)
    {
        return !score.IsPassed && !score.Mods.HasFlag(Mods.NoFail);
    }
}