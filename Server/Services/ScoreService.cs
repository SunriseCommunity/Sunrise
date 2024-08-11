using osu.Shared;
using Sunrise.Server.Data;
using Sunrise.Server.Helpers;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.Models;
using Sunrise.Server.Objects.Serializable;
using Sunrise.Server.Repositories.Chat;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Services;

public static class ScoreService
{
    public static async Task<string> SubmitScore(HttpRequest request)
    {
        var data = new SubmitScoreRequest(request);

        data.ThrowIfHasEmptyFields();

        var beatmap = await BeatmapService.GetBeatmap(data.BeatmapHash!);

        if (beatmap == null)
        {
            throw new Exception("Invalid request: BeatmapFile not found");
        }

        var decryptedScore = Parsers.ParseSubmittedScore(data);

        var score = await new Score().SetScoreFromString(decryptedScore, beatmap, data.OsuVersion ?? "");

        if (IsHasInvalidMods(score.Mods))
        {
            return "error: no";
        }

        var database = ServicesProviderHolder.ServiceProvider.GetRequiredService<SunriseDb>();

        var rawScores = await database.GetBeatmapScores(score.BeatmapHash, score.GameMode);
        var scores = new ScoresHelper(rawScores);

        var userStats = await database.GetUserStats(score.UserId, score.GameMode);
        var user = await database.GetUser(score.UserId);

        if (userStats == null)
        {
            throw new Exception("Invalid request: UserStats not found");
        }

        var prevUserStats = userStats.Clone();
        var prevPBest = scores.GetPersonalBestOf(score.UserId);

        var prevUserRank = await database.GetUserRank(userStats.UserId, userStats.GameMode);
        prevUserStats.Rank = prevUserRank;

        var timeElapsed = GetTimeElapsed(score, data);
        await userStats.UpdateWithScore(score, prevPBest, timeElapsed);

        if (IsScoreFailed(score))
        {
            await database.UpdateUserStats(userStats);
            return "error: no"; // Don't submit failed scores
        }

        var replayFile = await database.UploadReplay(userStats.UserId, data.Replay!);
        score.ReplayFileId = replayFile.Id;

        await database.InsertScore(score);
        await database.UpdateUserStats(userStats);

        var newPBest = scores.GetNewPersonalScore(score);
        userStats.Rank = await database.GetUserRank(userStats.UserId, userStats.GameMode);

        // Can be simplified to just get beatmapset by hash with full and use it for beatmap too
        var beatmapSet = await BeatmapService.GetBeatmapSet(beatmapSetId: beatmap.BeatmapsetId);

        if (newPBest.LeaderboardRank == 1)
        {
            var channels = ServicesProviderHolder.ServiceProvider.GetRequiredService<ChannelRepository>();
            var message = $"[https://osu.{Configuration.Domain}/{userStats.UserId} {user?.Username}] achieved #1 on [{beatmap.Url.Replace("ppy.sh", Configuration.Domain)} {beatmapSet?.Artist} - {beatmapSet?.Title} [{beatmap.Version}]] with {score.Accuracy:0.00}% accuracy for {score.PerformancePoints:0.00}pp!";
            channels.GetChannel("#announce")!.SendToChannel(message);
        }

        return GetScoreSubmitResponse(beatmap, userStats, prevUserStats, newPBest, prevPBest);
    }

    public static async Task<string> GetBeatmapScores(HttpRequest request)
    {
        var data = new GetScoresRequest(request);

        data.ThrowIfHasEmptyFields();

        var database = ServicesProviderHolder.ServiceProvider.GetRequiredService<SunriseDb>();

        var user = await database.GetUser(username: data.Username);

        if (user == null)
        {
            return $"{(int)BeatmapStatus.NotSubmitted}|false";
        }

        var rawScores = await database.GetBeatmapScores(data.Hash!, data.Mode, data.LeaderboardType, data.Mods, user);
        var scores = new ScoresHelper(rawScores);

        var beatmap = await BeatmapService.GetBeatmap(data.Hash!);

        if (beatmap == null)
        {
            if (data.BeatmapSetId == null)
            {
                return $"{(int)BeatmapStatus.NotSubmitted}|false";
            }

            // Can be simplified to just get beatmapset by hash with full and use it for beatmap too
            var beatmapSet = await BeatmapService.GetBeatmapSet(beatmapSetId: int.Parse(data.BeatmapSetId));

            return beatmapSet == null ? $"{(int)BeatmapStatus.NotSubmitted}|false" : $"{(int)BeatmapStatus.NeedsUpdate}|false";
        }

        if (beatmap.Status < BeatmapStatus.Ranked)
        {
            return $"{(int)beatmap.Status}|false";
        }

        var responses = new List<string>
        {
            $"{(int)beatmap.Status}|false|{beatmap.Id}|{beatmap.BeatmapsetId}|{scores.Count}",
            $"0\n{data.BeatmapName?.Replace(".osu", "")}\n10.0"
        };


        if (scores.Count == 0)
        {
            return string.Join("\n", responses);
        }

        var personalBest = scores.GetPersonalBestOf(user.Id);
        responses.Add(personalBest != null ? await personalBest.GetString() : "");

        var leaderboardScores = scores.GetTopScores(50);

        foreach (var score in leaderboardScores)
        {
            responses.Add(await score.GetString());
        }

        return string.Join("\n", responses);
    }

    private static string GetScoreSubmitResponse(Beatmap beatmap, UserStats user, UserStats prevUser, Score newScore, Score? prevScore)
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
            var lowerFirst = char.ToLower(entry[0]) + entry[1..];

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

    private static bool IsHasInvalidMods(Mods mods)
    {
        return mods.HasFlag(Mods.Relax) || mods.HasFlag(Mods.Autoplay) || mods.HasFlag(Mods.Target) ||
               mods.HasFlag(Mods.ScoreV2);
    }

    private static int GetTimeElapsed(Score score, SubmitScoreRequest data)
    {
        var isPassed = score.IsPassed || score.Mods.HasFlag(Mods.NoFail);

        if (string.IsNullOrEmpty(data.ScoreTime) || string.IsNullOrEmpty(data.ScoreFailTime))
        {
            return 0;
        }

        return isPassed ? int.Parse(data.ScoreTime) : int.Parse(data.ScoreFailTime);
    }

    private static bool IsScoreFailed(Score score)
    {
        return !score.IsPassed && !score.Mods.HasFlag(Mods.NoFail);
    }
}