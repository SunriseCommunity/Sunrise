using Sunrise.Server.Application;
using Sunrise.Server.Database;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Extensions;

public static class ScoreExtensions
{
    public static T? GetPersonalBestOf<T>(this List<T> scores, int userId) where T : Score
    {
        return scores.GetScoresGroupedByUsersBest().Find(x => x.UserId == userId);
    }

    public static int GetLeaderboardRankOf<T>(this List<Score> scores, Score score) where T : Score
    {
        if (scores.Find(x => x.UserId == score.UserId) == null)
        {
            scores = scores.UpsertUserScoreToSortedScores(score);
        }

        return scores.IndexOf(score) + 1;
    }

    public static async Task<int> GetLeaderboardRank(this Score score)
    {
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        return await database.ScoreService.GetLeaderboardRank(score);
    }

    public static List<T> GetScoresGroupedByUsersBest<T>(this List<T> scores) where T : Score
    {
        return GroupScoresByUserId(scores).Select(x => x.OrderByDescending(y => y.TotalScore).First()).ToList();
    }

    public static IEnumerable<IGrouping<int, T>> GroupScoresByUserId<T>(this List<T> scores) where T : Score
    {
        return scores.GroupBy(x => x.UserId);
    }

    public static List<T> SortScoresByTotalScore<T>(this List<T> scores) where T : Score
    {
        scores.Sort((x, y) =>
            y.TotalScore.CompareTo(x.TotalScore) != 0
                ? y.TotalScore.CompareTo(x.TotalScore)
                : x.WhenPlayed.CompareTo(y.WhenPlayed));
        return scores;
    }

    public static List<T> SortScoresByPerformancePoints<T>(this List<T> scores) where T : Score
    {
        scores.Sort((x, y) =>
            y.PerformancePoints.CompareTo(x.PerformancePoints) != 0
                ? y.PerformancePoints.CompareTo(x.PerformancePoints)
                : x.WhenPlayed.CompareTo(y.WhenPlayed));
        return scores;
    }

    public static List<T> UpsertUserScoreToSortedScores<T>(this List<T> scores, T score) where T : Score
    {
        var leaderboard = GetScoresGroupedByUsersBest(scores);

        var oldPBest = leaderboard.FindAll(x => x.UserId == score.UserId);

        foreach (var oldScore in oldPBest)
        {
            scores.Remove(oldScore);
        }


        leaderboard.Add(score);
        leaderboard = GetScoresGroupedByUsersBest(leaderboard);

        return leaderboard.ToList();
    }

    public static string ComputeOnlineHash(this Score score, string username, string clientHash, string? storyboardHash)
    {
        return string.Format(
            "chickenmcnuggets{0}o15{1}{2}smustard{3}{4}uu{5}{6}{7}{8}{9}{10}{11}Q{12}{13}{15}{14:yyMMddHHmmss}{16}{17}",
            score.Count300 + score.Count100,
            score.Count50,
            score.CountGeki,
            score.CountKatu,
            score.CountMiss,
            score.BeatmapHash,
            score.MaxCombo,
            score.Perfect,
            username,
            score.TotalScore,
            score.Grade,
            (int)score.Mods,
            score.IsPassed,
            (int)score.GameMode,
            score.ClientTime,
            score.OsuVersion,
            clientHash,
            storyboardHash).CreateMD5();
    }
}