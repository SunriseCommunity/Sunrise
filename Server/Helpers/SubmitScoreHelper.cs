using osu.Shared;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Objects.Serializable;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Helpers;

public static class SubmitScoreHelper
{
    public static string GetScoreSubmitResponse(Beatmap beatmap, UserStats user, UserStats prevUser, Score newScore, Score? prevScore)
    {
        var userUrl = $"https://{Configuration.Domain}/user/{user.Id}";

        // TODO: Change playcount and passcount to be from out db
        var beatmapInfo = $"beatmapId:{beatmap.Id}|beatmapSetId:{beatmap.BeatmapsetId}|beatmapPlaycount:{beatmap.Playcount}|beatmapPasscount:{beatmap.Passcount}|approvedDate:{beatmap.LastUpdated:yyyy-MM-dd}";
        var beatmapRanking = $"chartId:beatmap|chartUrl:{beatmap.Url}|chartName:Beatmap Ranking";
        var scoreInfo = string.Join("|", GetChart(prevScore, newScore));
        var playerInfo = $"chartId:overall|chartUrl:{userUrl}|chartName:Overall Ranking|" + string.Join("|", GetChart(prevUser, user));

        return $"{beatmapInfo}\n{beatmapRanking}|{scoreInfo}|onlineScoreId:{newScore.Id}\n{playerInfo}|achievements-new:";
    }

    public static bool IsHasInvalidMods(Mods mods)
    {
        // TODO: Support Relax and SCV2 at some point.
        return mods.HasFlag(Mods.Relax) || mods.HasFlag(Mods.Autoplay) || mods.HasFlag(Mods.Target) ||
               mods.HasFlag(Mods.ScoreV2);
    }

    public static int GetTimeElapsed(Score score, int scoreTime, int scoreFailTime)
    {
        var isPassed = score.IsPassed || score.Mods.HasFlag(Mods.NoFail);
        return isPassed ? scoreTime : scoreFailTime;
    }

    public static bool IsScoreFailed(Score score)
    {
        return !score.IsPassed && !score.Mods.HasFlag(Mods.NoFail);
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
}