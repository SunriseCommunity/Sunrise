using Sunrise.Server.Objects.Models;

namespace Sunrise.Server.Helpers;

public static class ScoreHelper
{
    public static List<Score> GetTopScores(this List<Score> scores, int? count = null)
    {
        var leaderboard = GetSortedScores(scores);

        var personalBests = new List<Score>();

        for (var i = 0; i < Math.Min(count ?? leaderboard.Count, leaderboard.Count); i++)
        {
            leaderboard[i].LeaderboardRank = i + 1;
            personalBests.Add(leaderboard[i]);
        }

        return personalBests;
    }

    public static Score GetNewPersonalScore(this List<Score> scores, Score score)
    {
        var leaderboard = GetSortedScores(scores);

        var oldPBest = leaderboard.Find(x => x.UserId == score.UserId);

        if (oldPBest != null)
        {
            leaderboard.Remove(oldPBest);
        }

        leaderboard.Add(score);
        leaderboard = GetSortedScores(leaderboard);

        var newPBest = leaderboard.Find(x => x.UserId == score.UserId);
        newPBest!.LeaderboardRank = leaderboard.IndexOf(newPBest) + 1;

        return newPBest;
    }

    public static Score? GetPersonalBestOf(this List<Score> scores, int userId)
    {
        return scores.GetTopScores().Find(x => x.UserId == userId);
    }

    public static List<Score> GetSortedScores(this List<Score> scores, bool onlyBest = true)
    {
        scores.Sort((x, y) =>
            y.TotalScore.CompareTo(x.TotalScore) != 0
                ? y.TotalScore.CompareTo(x.TotalScore)
                : x.WhenPlayed.CompareTo(y.WhenPlayed));

        return onlyBest ? scores.GroupBy(x => x.UserId).Select(x => x.OrderByDescending(y => y.TotalScore).First()).ToList() : scores;
    }
}