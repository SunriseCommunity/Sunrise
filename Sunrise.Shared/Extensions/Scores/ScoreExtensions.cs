using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using osu.Shared;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Beatmap;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Keys;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Services;
using Sunrise.Shared.Utils;
using Sunrise.Shared.Utils.Calculators;
using Sunrise.Shared.Utils.Converters;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Extensions.Scores;

public static class ScoreExtensions
{
    public static UserPersonalBestScores? GetUserPersonalBestScores(this List<Score> scores, int userId)
    {
        var personalBestByScoreValue = scores.GetScoresGroupedByUsersBest().Find(x => x.UserId == userId);
        if (personalBestByScoreValue == null)
            return null;

        var personalBestByPerformancePoints =
            Configuration.UseNewPerformanceCalculationAlgorithm ? scores.GetScoresGroupedByUsersBest(basedByPerformance: true).Find(x => x.UserId == userId) : null;

        return new UserPersonalBestScores(personalBestByScoreValue, personalBestByPerformancePoints);
    }

    public static List<T> GetScoresGroupedByUsersBest<T>(this List<T> scores, bool? basedByPerformance = null) where T : Score
    {
        return scores.GroupScoresByUserId()
            .Select(x => x.ToList()
                .GroupScoresByBeatmapId()
                .Select(y =>
                {
                    var groupedScores = y.ToList();

                    return basedByPerformance == true
                        ? groupedScores.SortScoresByPerformancePoints().First()
                        : groupedScores.SortScoresByTheirScoreValue().First();
                }))
            .SelectMany(x => x)
            .ToList();
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
        {
            var scoreComparison = y.TotalScore.CompareTo(x.TotalScore);
            return scoreComparison != 0 ? scoreComparison : CompareScoreTie(x, y);
        });
        return scores;
    }

    public static List<T> SortScoresByPerformancePoints<T>(this List<T> scores) where T : Score
    {
        scores.Sort((x, y) =>
        {
            var performanceComparison = y.PerformancePoints.CompareTo(x.PerformancePoints);
            return performanceComparison != 0 ? performanceComparison : CompareScoreTie(x, y);
        });
        return scores;
    }

    private static int CompareScoreTie(Score x, Score y)
    {
        var timeComparison = x.WhenPlayed.CompareTo(y.WhenPlayed);
        if (timeComparison != 0)
            return timeComparison;

        if (x.Id <= 0)
            return y.Id <= 0 ? 0 : 1;

        if (y.Id <= 0)
            return -1;

        return x.Id.CompareTo(y.Id);
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

    public static Score ToScore(this SubmittedScore baseScore, int userId, Beatmap beatmap, int timeElapsed)
    {
        var score = new Score
        {
            BeatmapHash = baseScore.BeatmapHash,
            UserId = userId,
            BeatmapId = beatmap.Id,
            ScoreHash = baseScore.ScoreHash,
            Count300 = baseScore.Count300,
            Count100 = baseScore.Count100,
            Count50 = baseScore.Count50,
            CountGeki = baseScore.CountGeki,
            CountKatu = baseScore.CountKatu,
            CountMiss = baseScore.CountMiss,
            TotalScore = baseScore.TotalScore,
            MaxCombo = baseScore.MaxCombo,
            Perfect = baseScore.Perfect,
            Grade = baseScore.Grade,
            Mods = baseScore.Mods,
            IsPassed = baseScore.IsPassed,
            IsScoreable = beatmap.IsScoreable,
            GameMode = baseScore.GameMode,
            WhenPlayed = baseScore.WhenPlayed,
            OsuVersion = baseScore.OsuVersion,
            BeatmapHashStatus = new BeatmapHashStatus
            {
                BeatmapHash = beatmap.Checksum!,
                BeatmapId = beatmap.Id,
                Status = beatmap.Status
            },
            ClientTime = baseScore.ClientTime,
            Accuracy = baseScore.Accuracy,
            TimeElapsed = timeElapsed
        };

        return score;
    }

    public static Result<SubmittedScore> TryParseBaseScore(this string scoreString, DateTime scoreSubmittedAt)
    {
        if (string.IsNullOrWhiteSpace(scoreString) || !scoreString.Contains(':'))
            return Result.Failure<SubmittedScore>("Invalid score string format");

        var split = scoreString.Split(':');

        if (split.Length < 18)
            return Result.Failure<SubmittedScore>("Invalid score string format");

        try
        {
            var score = new SubmittedScore
            {
                BeatmapHash = string.IsNullOrWhiteSpace(split[0]) ? throw new Exception("Beatmap hash is empty") : split[0],
                PlayerUsername = string.IsNullOrWhiteSpace(split[1]) ? throw new Exception("Player username is empty") : split[1],
                ScoreHash = string.IsNullOrWhiteSpace(split[2]) ? throw new Exception("Score hash is empty") : split[2],
                Count300 = ParseStableInt(split[3], nameof(SubmittedScore.Count300)),
                Count100 = ParseStableInt(split[4], nameof(SubmittedScore.Count100)),
                Count50 = ParseStableInt(split[5], nameof(SubmittedScore.Count50)),
                CountGeki = ParseStableInt(split[6], nameof(SubmittedScore.CountGeki)),
                CountKatu = ParseStableInt(split[7], nameof(SubmittedScore.CountKatu)),
                CountMiss = ParseStableInt(split[8], nameof(SubmittedScore.CountMiss)),
                TotalScore = ParseStableInt(split[9], nameof(SubmittedScore.TotalScore)),
                MaxCombo = ParseStableInt(split[10], nameof(SubmittedScore.MaxCombo)),
                Perfect = bool.Parse(split[11]),
                Grade = ScoreGradeUtil.TryParse(split[12], out var grade) ? grade : throw new Exception("Invalid grade"),
                Mods = (Mods)int.Parse(split[13]),
                IsPassed = bool.Parse(split[14]),
                GameMode = ParseVanillaGameMode(split[15]),
                WhenPlayed = scoreSubmittedAt,
                OsuVersion = string.IsNullOrWhiteSpace(split[17]) ? throw new Exception("Osu version is empty") : split[17].Trim(),
                ClientTime = DateTime.ParseExact(split[16], "yyMMddHHmmss", null),
                Accuracy = 0
            };

            if (!ScoreSubmissionVersionValidator.IsValid(score.OsuVersion))
                throw new Exception("Invalid osu! version");

            score.GameMode = score.GameMode.EnrichWithMods(score.Mods);
            score.Accuracy = PerformanceCalculator.CalculateAccuracy(score);

            return score;
        }
        catch (Exception ex)
        {
            return Result.Failure<SubmittedScore>($"Error parsing score string: {ex.Message}");
        }
    }

    private static int ParseStableInt(string value, string fieldName)
    {
        return int.TryParse(value, out var parsed) && parsed >= 0
            ? parsed
            : throw new Exception($"{fieldName} must be between 0 and {int.MaxValue}");
    }

    private static GameMode ParseVanillaGameMode(string value)
    {
        if (!int.TryParse(value, out var parsed) || parsed is < 0 or > 3)
            throw new Exception("Unsupported game mode");

        return (GameMode)parsed;
    }

    public static string ToScoreString(this Score score, string userUsername)
    {
        return string.Join(":",
            score.BeatmapHash,
            userUsername,
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


    public static List<(T Score, int? LeaderboardPosition)> WithLeaderboardPositions<T>(this List<T> scores) where T : Score
    {
        return scores.Select((score, index) => (score, (int?)(index + 1))).ToList();
    }


    /// <summary>
    ///     Used in leaderboard responses.
    /// </summary>
    /// <param name="score"></param>
    /// <returns></returns>
    public static string GetString(this Score score, int? leaderboardPosition)
    {
        var time = (int)score.WhenPlayed.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        var hasReplay = score.ReplayFileId != null ? "1" : "0";

        // If the game mode is not scoreable, we should return the performance points instead of the total score
        var totalScore = score.GameMode.IsGameModeWithoutScoreMultiplier() ? (int)score.PerformancePoints : score.TotalScore;

        return
            $"{score.Id}|{score.User.Username}|{totalScore}|{score.MaxCombo}|{score.Count50}|{score.Count100}|{score.Count300}|{score.CountMiss}|{score.CountKatu}|{score.CountGeki}|{score.Perfect}|{(int)score.Mods}|{score.UserId}|{leaderboardPosition}|{time}|{hasReplay}";
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

    public static async Task<string> GetBeatmapInGameChatString(this Score score, BeatmapSet beatmapSet, BaseSession session)
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
                SunriseMetrics.RequestReturnedErrorCounterInc(RequestType.OsuSubmitScore, score.UserId, recalculateBeatmapResult.Error.Message);
            }
            else
            {
                beatmap.UpdateBeatmapWithPerformance(score.Mods, recalculateBeatmapResult.Value);
            }
        }

        return score.GetBeatmapInGameChatString(beatmapSet, beatmap);
    }

    public static string GetBeatmapInGameChatString(this Score score, BeatmapSet beatmapSet, Beatmap beatmap)
    {
        return $"{beatmap.GetBeatmapInGameChatString(beatmapSet)} {score.Mods.GetModsString()}| GameMode: {score.GameMode.ToVanillaGameMode()} | Acc: {score.Accuracy:0.00}% | {score.PerformancePoints:0.00}pp | {TimeConverter.SecondsToString(beatmap.TotalLength)} | {beatmap.DifficultyRating:0.00} ★";
    }
}
