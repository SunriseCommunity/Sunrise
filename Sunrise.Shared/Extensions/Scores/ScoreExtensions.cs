using Microsoft.Extensions.DependencyInjection;
using osu.Shared;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Objects.Keys;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Services;
using Sunrise.Shared.Utils.Calculators;
using Sunrise.Shared.Utils.Converters;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Extensions.Scores;

public static class ScoreExtensions
{
    public static T? GetPersonalBestOf<T>(this List<T> scores, int userId) where T : Score
    {
        return scores.GetScoresGroupedByUsersBest().Find(x => x.UserId == userId);
    }

    public static List<T> GetScoresGroupedByUsersBest<T>(this List<T> scores) where T : Score
    {
        return GroupScoresByUserId(scores)
            .Select(x => x.ToList()
                .GroupScoresByBeatmapId()
                .Select(y => y.OrderByDescending(z => z.GameMode.IsGameModeWithoutScoreMultiplier() ? z.PerformancePoints : z.TotalScore)
                    .First()))
            .SelectMany(x => x)
            .ToList();
    }

    public static List<T> GetScoresGroupedByBeatmapBest<T>(this List<T> scores) where T : Score
    {
        return GroupScoresByBeatmapId(scores)
            .Select(x => x.OrderByDescending(y => y.GameMode.IsGameModeWithoutScoreMultiplier() ? y.PerformancePoints : y.TotalScore).First()).ToList();
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

    /// <summary>
    ///     Sorts the scores by their score value.
    ///     Score value is determined if the game mode has a score multiplier or not.
    ///     For game modes with score multiplier, the scores are sorted by their total score.
    ///     For game modes without score multiplier, the scores are sorted by their performance points.
    /// </summary>
    /// <param name="scores"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static List<T> SortScoresByTheirScoreValue<T>(this List<T> scores) where T : Score
    {
        var isWithoutScoreMultiplier = scores.FirstOrDefault()?.GameMode.IsGameModeWithoutScoreMultiplier() ?? false;

        return isWithoutScoreMultiplier
            ? scores.SortScoresByPerformancePoints()
            : scores.SortScoresByTotalScore();
    }

    public static List<T> UpsertUserScoreToSortedScores<T>(this List<T> scores, T score) where T : Score
    {
        var leaderboard = GetScoresGroupedByUsersBest(scores);

        var oldScores = leaderboard.FindAll(x => x.UserId == score.UserId && x.BeatmapHash == score.BeatmapHash && x.GameMode == score.GameMode);

        foreach (var oldScore in oldScores)
        {
            leaderboard.Remove(oldScore);
        }

        leaderboard.Add(score);
        leaderboard = GetScoresGroupedByUsersBest(leaderboard);
        leaderboard = leaderboard.SortScoresByTheirScoreValue();
        leaderboard = leaderboard.EnrichWithLeaderboardPositions();

        return leaderboard.ToList();
    }

    public static Score TryParseToSubmittedScore(this string scoreString, Session session, Beatmap beatmap)
    {
        var split = scoreString.Split(':');

        var score = new Score
        {
            BeatmapHash = split[0],
            UserId = session.UserId,
            BeatmapId = beatmap.Id,
            ScoreHash = split[2],
            Count300 = int.Parse(split[3]),
            Count100 = int.Parse(split[4]),
            Count50 = int.Parse(split[5]),
            CountGeki = int.Parse(split[6]),
            CountKatu = int.Parse(split[7]),
            CountMiss = int.Parse(split[8]),
            TotalScore = long.Parse(split[9]),
            MaxCombo = int.Parse(split[10]),
            Perfect = bool.Parse(split[11]),
            Grade = split[12],
            Mods = (Mods)int.Parse(split[13]),
            IsPassed = bool.Parse(split[14]),
            IsScoreable = beatmap.IsScoreable,
            GameMode = (GameMode)int.Parse(split[15]),
            WhenPlayed = DateTime.UtcNow,
            OsuVersion = split[17].Trim(),
            BeatmapStatus = beatmap.Status,
            ClientTime = DateTime.ParseExact(split[16], "yyMMddHHmmss", null)
        };

        score.LocalProperties = score.LocalProperties.FromScore(score);
        score.GameMode = score.GameMode.EnrichWithMods(score.Mods);
        score.Accuracy = PerformanceCalculator.CalculateAccuracy(score);

        using var scope = ServicesProviderHolder.CreateScope();
        var calculatorService = scope.ServiceProvider.GetRequiredService<CalculatorService>();

        var scorePerformanceResult = calculatorService.CalculateScorePerformance(session, score).Result;

        if (scorePerformanceResult.IsFailure)
        {
            SunriseMetrics.RequestReturnedErrorCounterInc(RequestType.OsuSubmitScore, session, scorePerformanceResult.Error.Message);
            score.PerformancePoints = 0;
        }
        else
        {
            score.PerformancePoints = scorePerformanceResult.Value.PerformancePoints;
        }

        return score;
    }

    public static string ToScoreString(this Score score)
    {
        return string.Join(":",
            score.BeatmapHash,
            score.UserId.ToString(),
            score.ScoreHash,
            score.Count300.ToString(),
            score.Count100.ToString(),
            score.Count50.ToString(),
            score.CountGeki.ToString(),
            score.CountKatu.ToString(),
            score.CountMiss.ToString(),
            score.TotalScore.ToString(),
            score.MaxCombo.ToString(),
            score.Perfect.ToString(),
            score.Grade,
            ((int)score.Mods).ToString(),
            score.IsPassed.ToString(),
            ((int)score.GameMode.ToVanillaGameMode()).ToString(),
            score.ClientTime.ToString("yyMMddHHmmss"),
            score.OsuVersion);
    }


    public static List<T> EnrichWithLeaderboardPositions<T>(this List<T> scores) where T : Score
    {
        for (var i = 0; i < scores.Count; i++)
        {
            scores[i].LocalProperties.LeaderboardPosition = i + 1;
        }

        return scores;
    }


    /// <summary>
    ///     Used in leaderboard responses.
    /// </summary>
    /// <param name="score"></param>
    /// <returns></returns>
    public static string GetString(this Score score)
    {
        var time = (int)score.WhenPlayed.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        var hasReplay = score.ReplayFileId != null ? "1" : "0";

        // If the game mode is not scoreable, we should return the performance points instead of the total score
        var totalScore = score.GameMode.IsGameModeWithoutScoreMultiplier() ? (int)score.PerformancePoints : score.TotalScore;

        return
            $"{score.Id}|{score.User.Username}|{totalScore}|{score.MaxCombo}|{score.Count50}|{score.Count100}|{score.Count300}|{score.CountMiss}|{score.CountKatu}|{score.CountGeki}|{score.Perfect}|{(int)score.Mods}|{score.UserId}|{score.LocalProperties.LeaderboardPosition}|{time}|{hasReplay}";
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
            (int)score.GameMode.ToVanillaGameMode(),
            score.ClientTime,
            score.OsuVersion,
            clientHash,
            storyboardHash).ToHash();
    }

    public static async Task<string> GetBeatmapInGameChatString(this Score score, BeatmapSet beatmapSet, Session session)
    {
        var beatmap = beatmapSet.Beatmaps.FirstOrDefault(b => b.Id == score.BeatmapId);
        if (beatmap == null)
            return "Beatmap not found while trying to get information string for score";

        using var scope = ServicesProviderHolder.CreateScope();
        var calculatorService = scope.ServiceProvider.GetRequiredService<CalculatorService>();

        if ((int)score.GameMode != beatmap.ModeInt || (int)score.Mods > 0)
        {
            var recalculateBeatmapResult = await calculatorService.CalculateBeatmapPerformance(session, score.BeatmapId, score.GameMode, score.Mods);

            if (recalculateBeatmapResult.IsFailure)
            {
                SunriseMetrics.RequestReturnedErrorCounterInc(RequestType.OsuSubmitScore, session, recalculateBeatmapResult.Error.Message);
            }
            else
            {
                beatmap.UpdateBeatmapWithPerformance(score.Mods, recalculateBeatmapResult.Value);
            }
        }

        return $"{beatmap.GetBeatmapInGameChatString(beatmapSet)} {score.Mods.GetModsString()}| GameMode: {score.GameMode.ToVanillaGameMode()} | Acc: {score.Accuracy:0.00}% | {score.PerformancePoints:0.00}pp | {TimeConverter.SecondsToString(beatmap.TotalLength)} | {beatmap.DifficultyRating:0.00} ★";
    }
}