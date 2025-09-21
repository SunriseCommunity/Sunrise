using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions.Beatmaps;

namespace Sunrise.Shared.Database.Extensions;

public class BeatmapPlaycount
{
    public int Key { get; set; }
    public int Count { get; set; }
    public DateTime WhenPlayed { get; set; }
}

public static class ScoreQueryableExtensions
{
    public static IQueryable<Score> FilterValidScores(this IQueryable<Score> queryable)
    {
        return queryable.Where(s => s.User.AccountStatus != UserAccountStatus.Restricted &&
                                    s.SubmissionStatus != SubmissionStatus.Deleted &&
                                    s.SubmissionStatus != SubmissionStatus.Unknown &&
                                    s.BeatmapStatus != BeatmapStatus.Unknown);
    }

    public static IQueryable<Score> FilterPassedRankedScores(this IQueryable<Score> queryable)
    {
        return queryable
            .FilterPassedScoreableScores()
            .Where(s => s.BeatmapStatus == BeatmapStatus.Ranked || s.BeatmapStatus == BeatmapStatus.Approved);
    }

    public static IQueryable<Score> FilterPassedScoreableScores(this IQueryable<Score> queryable)
    {
        return queryable.Where(s => s.IsScoreable && s.IsPassed && s.SubmissionStatus != SubmissionStatus.Failed);
    }

    public static IQueryable<Score> SelectBeatmapsBestScores(this IQueryable<Score> queryable)
    {
        var gameModesWithoutScoreMultiplier = GameModeExtensions.GetGameModesWithoutScoreMultiplier();

        return queryable
            .Where(x => x.SubmissionStatus == SubmissionStatus.Best)
            .GroupBy(x => x.BeatmapId)
            .Select(g =>
                g.OrderByDescending(x =>
                    EF.Constant(gameModesWithoutScoreMultiplier).Contains(x.GameMode) ? x.PerformancePoints : x.TotalScore
                ).First());
    }

    public static IQueryable<Score> SelectUsersPersonalBestScores(this IQueryable<Score> queryable, bool rankByPerformancePoints = false)
    {
        var gameModesWithoutScoreMultiplier = GameModeExtensions.GetGameModesWithoutScoreMultiplier();

        return queryable
            .Where(x => rankByPerformancePoints || x.SubmissionStatus == SubmissionStatus.Best) // Ignore submission status when ranking by pp
            .GroupBy(x => new
            {
                x.UserId,
                x.BeatmapId
            })
            .Select(g =>
                g.OrderByDescending(x =>
                    rankByPerformancePoints == true || EF.Constant(gameModesWithoutScoreMultiplier).Contains(x.GameMode) ? x.PerformancePoints : x.TotalScore
                ).First());
    }

    public static IQueryable<Score> OrderByScoreValueDescending(this IQueryable<Score> queryable)
    {
        var gameModesWithoutScoreMultiplier = GameModeExtensions.GetGameModesWithoutScoreMultiplier();

        return queryable
            .OrderByDescending(x => EF.Constant(gameModesWithoutScoreMultiplier).Contains(x.GameMode) ? x.PerformancePoints : x.TotalScore)
            .ThenByDescending(x => x.WhenPlayed);
    }

    public static IQueryable<BeatmapPlaycount> GroupScoresByBeatmapPlaycount(this IQueryable<Score> queryable)
    {
        return queryable.GroupBy(x => x.BeatmapId)
            .Select(g => new BeatmapPlaycount
            {
                Key = g.Key,
                Count = g.Count(),
                WhenPlayed = g.Max(x => x.WhenPlayed)
            });
    }

    public static IQueryable<Score> IncludeUser(this IQueryable<Score> queryable)
    {
        return queryable
            .Include(x => x.User)
            .Include(y => y.User.UserFiles.Where(f => f.Type == FileType.Avatar || f.Type == FileType.Banner));
    }
}