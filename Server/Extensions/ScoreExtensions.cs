using osu.Shared;
using Sunrise.Server.Application;
using Sunrise.Server.Database;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.Serializable;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Extensions;

public static class ScoreExtensions
{
    public static T? GetPersonalBestOf<T>(this List<T> scores, int userId) where T : Score
    {
        return scores.GetScoresGroupedByUsersBest().Find(x => x.UserId == userId);
    }

    public static int GetLeaderboardRankOf<T>(this List<T> scores, T score) where T : Score
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
        // TODO: Should support multiple leaderboard systems
        var scores = await database.ScoreService.GetBeatmapScores(score.BeatmapHash, score.GameMode);

        return scores.GetLeaderboardRankOf(score);
    }

    public static List<T> GetScoresGroupedByUsersBest<T>(this List<T> scores) where T : Score
    {
        return GroupScoresByUserId(scores)
            .Select(x => x.ToList()
                .GroupScoresByBeatmapId()
                .Select(y => y.OrderByDescending(z => z.TotalScore)
                    .First()))
            .SelectMany(x => x)
            .ToList();
    }

    public static List<T> GetScoresGroupedByBeatmapBest<T>(this List<T> scores) where T : Score
    {
        return GroupScoresByBeatmapId(scores).Select(x => x.OrderByDescending(y => y.TotalScore).First()).ToList();
    }

    public static IEnumerable<IGrouping<int, T>> GroupScoresByBeatmapId<T>(this List<T> scores) where T : Score
    {
        return scores.GroupBy(x => x.BeatmapId);
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

        var oldScores = leaderboard.FindAll(x => x.UserId == score.UserId && x.BeatmapHash == score.BeatmapHash && x.GameMode == score.GameMode); 
        foreach (var oldScore in oldScores)
        {
            scores.Remove(oldScore);
        }
        
        leaderboard.Add(score);
        leaderboard = GetScoresGroupedByUsersBest(leaderboard);
        leaderboard = leaderboard.SortScoresByTotalScore();
        leaderboard = leaderboard.UpdateLeaderboardPositions();

        return leaderboard.ToList();
    }

    public static Score TryParseToSubmittedScore(this string scoreString, Session session, Beatmap beatmap)
    {
        var split = scoreString.Split(':');

        var score = new Score
        {
            BeatmapHash = split[0],
            UserId = session.User.Id,
            BeatmapId = beatmap.Id,
            ScoreHash = split[2],
            Count300 = int.Parse(split[3]),
            Count100 = int.Parse(split[4]),
            Count50 = int.Parse(split[5]),
            CountGeki = int.Parse(split[6]),
            CountKatu = int.Parse(split[7]),
            CountMiss = int.Parse(split[8]),
            TotalScore = int.Parse(split[9]),
            MaxCombo = int.Parse(split[10]),
            Perfect = bool.Parse(split[11]),
            Grade = split[12],
            Mods = (Mods)int.Parse(split[13]),
            IsPassed = bool.Parse(split[14]),
            IsScoreable = beatmap.IsScoreable,
            GameMode = (GameMode)int.Parse(split[15]),
            WhenPlayed = DateTime.UtcNow,
            OsuVersion = split[17],
            BeatmapStatus = beatmap.Status,
            ClientTime = DateTime.ParseExact(split[16], "yyMMddHHmmss", null)
        };

        score.Accuracy = Calculators.CalculateAccuracy(score);
        score.PerformancePoints = Calculators.CalculatePerformancePoints(session, score);

        return score;
    }

    public static List<T> UpdateLeaderboardPositions<T>(this List<T> scores) where T : Score
    {
        for (var i = 0; i < scores.Count; i++)
        {
            scores[i].LocalProperties.LeaderboardPosition = i + 1;
        }

        return scores;
    }

    public static async Task<string> GetString(this Score score)
    {
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var time = (int)score.WhenPlayed.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        var username = (await database.UserService.GetUser(score.UserId))?.Username ?? "Unknown";
        var hasReplay = score.ReplayFileId != null ? "1" : "0";

        return
            $"{score.Id}|{username}|{score.TotalScore}|{score.MaxCombo}|{score.Count50}|{score.Count100}|{score.Count300}|{score.CountMiss}|{score.CountKatu}|{score.CountGeki}|{score.Perfect}|{(int)score.Mods}|{score.UserId}|{score.LocalProperties.LeaderboardPosition}|{time}|{hasReplay}";
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