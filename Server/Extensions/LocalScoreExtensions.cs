using osu.Shared;
using Sunrise.Server.Application;
using Sunrise.Server.Database;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.Serializable;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Extensions;

public static class LocalScoreExtensions
{
    public static List<LocalScore> ToLocalScores(this List<Score> scores)
    {
        return scores.Select((x, i) => new LocalScore
        {
            Id = x.Id,
            UserId = x.UserId,
            BeatmapId = x.BeatmapId,
            ScoreHash = x.ScoreHash,
            BeatmapHash = x.BeatmapHash,
            ReplayFileId = x.ReplayFileId,
            TotalScore = x.TotalScore,
            MaxCombo = x.MaxCombo,
            Count300 = x.Count300,
            Count100 = x.Count100,
            Count50 = x.Count50,
            CountMiss = x.CountMiss,
            CountKatu = x.CountKatu,
            CountGeki = x.CountGeki,
            Perfect = x.Perfect,
            LeaderboardPosition = i + 1
        }).ToList();
    }

    public static LocalScore TryParseToSubmittedScore(this string scoreString, Session session, Beatmap beatmap)
    {
        var split = scoreString.Split(':');

        var score = new LocalScore
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
            IsRanked = beatmap.IsScoreable,
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

    public static async Task<string> GetString(this LocalScore score)
    {
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var time = (int)score.WhenPlayed.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        var username = (await database.UserService.GetUser(score.UserId))?.Username ?? "Unknown";
        var hasReplay = score.ReplayFileId != null ? "1" : "0";

        return
            $"{score.Id}|{username}|{score.TotalScore}|{score.MaxCombo}|{score.Count50}|{score.Count100}|{score.Count300}|{score.CountMiss}|{score.CountKatu}|{score.CountGeki}|{score.Perfect}|{(int)score.Mods}|{score.UserId}|{score.LeaderboardPosition}|{time}|{hasReplay}";
    }
}