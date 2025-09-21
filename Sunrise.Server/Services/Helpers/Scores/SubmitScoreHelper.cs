using osu.Shared;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Extensions.Scores;
using Sunrise.Shared.Extensions.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Keys;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Objects.Sessions;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;

namespace Sunrise.Server.Services.Helpers.Scores;

public static class SubmitScoreHelper
{
    private const string MetricsError = "Score {0} by (user id: {1}) rejected with reason: {2}";
    private const string AnnounceNewFirstPlaceString = "{0} achieved #1 on {1}";


    public static string GetNewFirstPlaceString(Session session, Score score, BeatmapSet beatmapSet, Beatmap beatmap)
    {
        var scoreMessage = score.GetBeatmapInGameChatString(beatmapSet, session).Result;
        var message = string.Format(AnnounceNewFirstPlaceString, score.User.GetUserInGameChatString(), scoreMessage);

        return message;
    }

    public static void ReportRejectionToMetrics(Session session, string scoreData, string reason)
    {
        var message = string.Format(MetricsError, scoreData, session.UserId, reason);
        SunriseMetrics.RequestReturnedErrorCounterInc(RequestType.OsuSubmitScore, null, message);
    }

    public static void UpdateSubmissionStatus(this Score score, Score? prevPBest)
    {
        if (IsScoreFailed(score))
        {
            score.SubmissionStatus = SubmissionStatus.Failed;
            return;
        }

        if (!score.IsScoreable)
        {
            score.SubmissionStatus = SubmissionStatus.Submitted;
            return;
        }

        var scores = new List<Score>
        {
            score
        };
        if (prevPBest != null)
            scores.Add(prevPBest);

        var bestScore = scores.SortScoresByTheirScoreValue().FirstOrDefault();

        if (bestScore == score)
        {
            score.SubmissionStatus = SubmissionStatus.Best;
            return;
        }

        score.SubmissionStatus = SubmissionStatus.Submitted;
    }

    public static bool IsScoreValid(Session session, Score score, string clientHash,
        string beatmapHash, string onlineBeatmapHash, string? storyboardHash)
    {
        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var user = database.Users.GetUser(id: score.UserId).Result;
        if (user == null)
            return false;

        var computedOnlineHash = score.ComputeOnlineHash(user.Username, clientHash, storyboardHash);

        var checks = new[]
        {
            string.Equals(clientHash, session.Attributes.UserHash, StringComparison.Ordinal),
            string.Equals(score.ScoreHash, computedOnlineHash, StringComparison.Ordinal),
            string.Equals(beatmapHash,
                onlineBeatmapHash,
                StringComparison
                    .Ordinal) // Since we got beatmap from client hash, this is not really needed. But just for obscure cases.
        };

        if (checks.All(x => x))
        {
            return true;
        }

        ReportRejectionToMetrics(session, $"{clientHash}|{session.Attributes.UserHash}|{score.ScoreHash}|{computedOnlineHash}|{beatmapHash}|{onlineBeatmapHash}.storyboard.{storyboardHash}", "Invalid checksums on score submission");
        return false;
    }

    public static string GetScoreSubmitResponse(Beatmap beatmap, UserStats userStats, UserStats prevUserStats,
        Score newScore,
        UserPersonalBestScores? prevUserPersonalBestScores, string? newAchievements = null)
    {
        var userUrl = $"https://{Configuration.Domain}/user/{userStats.UserId}";
        var dontShowPp = beatmap.Status != BeatmapStatus.Ranked && beatmap.Status != BeatmapStatus.Approved;

        var beatmapInfo =
            $"beatmapId:{beatmap.Id}|beatmapSetId:{beatmap.BeatmapsetId}|beatmapPlaycount:{beatmap.Playcount}|beatmapPasscount:{beatmap.Passcount}|approvedDate:{beatmap.LastUpdated:yyyy-MM-dd}";
        var beatmapRanking = $"chartId:beatmap|chartUrl:{beatmap.Url}|chartName:Beatmap Ranking";
        var scoreInfo = string.Join("|", GetChart(prevUserPersonalBestScores?.BestScoreBasedByTotalScore, prevUserPersonalBestScores?.BestScoreForPerformanceCalculation, newScore, dontShowPp));
        var playerInfo = $"chartId:overall|chartUrl:{userUrl}|chartName:Overall Ranking|" +
                         string.Join("|", GetChart(prevUserStats, null, userStats));

        return
            $"{beatmapInfo}\n{beatmapRanking}|{scoreInfo}|onlineScoreId:{newScore.Id}\n{playerInfo}|achievements-new:{newAchievements}";
    }

    public static bool IsHasInvalidMods(Mods mods)
    {
        return mods.HasFlag(Mods.Target) ||
               mods.HasFlag(Mods.Random) ||
               mods.HasFlag(Mods.KeyCoop) ||
               mods.HasFlag(Mods.Cinema) ||
               mods.HasFlag(Mods.Autoplay);
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

    private static List<string> GetChart<T>(T before, T? alternativeBeforeForPpEntry, T after, bool dontShowPp = false)
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
                "Rank" => typeof(T) == typeof(Score) ? "LocalProperties.LeaderboardPosition" : "LocalProperties.Rank",
                "Pp" => "PerformancePoints",
                _ => entry
            };

            var beforeValue = entry == "Pp" && alternativeBeforeForPpEntry != null ? GetPropertyValue(alternativeBeforeForPpEntry, obj) : GetPropertyValue(before, obj);
            var afterValue = GetPropertyValue(after, obj);

            if (dontShowPp && entry == "Pp")
            {
                beforeValue = null;
                afterValue = null;
            }

            result.Add(GetChartEntry(lowerFirst,
                beforeValue,
                afterValue));
        }

        return result;
    }

    private static object? GetPropertyValue(object? obj, string propertyPath)
    {
        if (obj == null || string.IsNullOrEmpty(propertyPath))
            return null;

        var properties = propertyPath.Split('.');

        foreach (var prop in properties)
        {
            if (obj == null) return null;

            var propertyInfo = obj.GetType().GetProperty(prop);
            if (propertyInfo == null)
                throw new ArgumentException($"Property {prop} not found in {obj.GetType().Name}");

            obj = propertyInfo.GetValue(obj);
        }

        return obj;
    }

    private static string GetChartEntry(string name, object? before, object? after)
    {
        return $"{name}Before:{before?.ToString() ?? string.Empty}|{name}After:{after?.ToString() ?? string.Empty}";
    }
}