using Sunrise.Server.Objects.Models;

namespace Sunrise.Server.Helpers;

public class ScoresHelper
{
    private static List<Score> _scores = [];

    public ScoresHelper(List<Score> scores)
    {
        _scores = GetSortedScores(scores);
    }

    public int Count => _scores.Count;

    public List<Score> GetTopScores(int? count = null)
    {
        var leaderboard = _scores;

        var personalBests = new List<Score>();

        for (var i = 0; i < Math.Min(count ?? leaderboard.Count, leaderboard.Count); i++)
        {
            leaderboard[i].LeaderboardRank = i + 1;
            personalBests.Add(leaderboard[i]);
        }

        return personalBests;
    }

    public Score GetNewPersonalScore(Score score)
    {
        var leaderboard = new List<Score>(_scores);

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

    public Score? GetPersonalBestOf(int userId)
    {
        return GetTopScores()?.Find(x => x.UserId == userId);
    }

    public static List<Score> GetSortedScores(List<Score> scores, bool onlyBest = true)
    {
        scores.Sort((x, y) =>
            y.TotalScore.CompareTo(x.TotalScore) != 0
                ? y.TotalScore.CompareTo(x.TotalScore)
                : x.WhenPlayed.CompareTo(y.WhenPlayed));

        return onlyBest ? scores.GroupBy(x => x.UserId).Select(x => x.OrderByDescending(y => y.TotalScore).First()).ToList() : scores;
    }
}