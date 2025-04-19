using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using osu.Shared;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Database.Services;
using Sunrise.Shared.Enums.Leaderboards;
using Sunrise.Shared.Utils;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Database.Repositories;

public class ScoreRepository(ILogger<ScoreRepository> logger, SunriseDbContext dbContext, ScoreFileService scoreFileService)
{

    public ScoreFileService Files { get; } = scoreFileService;

    public async Task<Result> AddScore(Score score)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            dbContext.Scores.Add(score);
            await dbContext.SaveChangesAsync();
        });
    }

    public async Task<Result> UpdateScore(Score score)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            dbContext.UpdateEntity(score);
            await dbContext.SaveChangesAsync();
        });
    }

    public async Task<Result> MarkScoreAsDeleted(Score score)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            score.SubmissionStatus = SubmissionStatus.Deleted;
            await UpdateScore(score);
        });
    }

    public async Task<List<Score>> GetBestScoresByGameMode(GameMode mode, QueryOptions? options = null)
    {
        var groupedBestScores = dbContext.Scores
            .FilterValidScores()
            .FilterPassedRankedScores()
            .Where(x => x.GameMode == EF.Constant(mode))
            .SelectUsersPersonalBestScores();

        var queryScore = dbContext.Scores
            .FromSqlRaw(groupedBestScores.ToQueryString())
            .OrderByDescending(x => x.PerformancePoints)
            .ThenByDescending(x => x.WhenPlayed)
            .UseQueryOptions(options);

        var queryResult = await queryScore.ToListAsync();

        return queryResult;
    }

    public async Task<Score?> GetScore(int id, QueryOptions? options = null)
    {
        return await dbContext.Scores
            .FilterValidScores()
            .Where(s => s.Id == id)
            .UseQueryOptions(options)
            .FirstOrDefaultAsync();
    }

    public async Task<Score?> GetScore(string scoreHash, QueryOptions? options = null)
    {
        return await dbContext.Scores
            .FilterValidScores()
            .Where(s => s.ScoreHash == scoreHash)
            .UseQueryOptions(options)
            .FirstOrDefaultAsync();
    }

    public async Task<(List<KeyValuePair<int, int>>, int)> GetUserMostPlayedBeatmapIds(int userId, GameMode mode, QueryOptions? options = null)
    {
        var groupedBeatmapsQuery = dbContext.Scores
            .FilterValidScores()
            .Where(s => s.UserId == userId && s.GameMode == mode)
            .GroupScoresByBeatmapPlaycount();

        var groupedBeatmapsCount = await groupedBeatmapsQuery.CountAsync();

        var mostPlayedBeatmaps = await groupedBeatmapsQuery
            .OrderByDescending(g => g.Count)
            .ThenByDescending(g => g.WhenPlayed)
            .UseQueryOptions(options)
            .Select(g => new KeyValuePair<int, int>(g.Key, g.Count))
            .ToListAsync();

        return (mostPlayedBeatmaps, groupedBeatmapsCount);
    }

    public async Task<Score?> GetUserLastScore(int userId, QueryOptions? options = null)
    {
        return await dbContext.Scores
            .FilterValidScores()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.WhenPlayed)
            .UseQueryOptions(options)
            .FirstOrDefaultAsync();
    }

    public async Task<(List<Score> Scores, int TotalCount)> GetBeatmapScores(string beatmapHash, GameMode gameMode,
        LeaderboardType type = LeaderboardType.Global, Mods? mods = null, User? user = null, QueryOptions? options = null)
    {
        var scoresGrouped = dbContext.Scores
            .FilterValidScores()
            .FilterPassedScoreableScores()
            .Where(
                s =>
                    s.BeatmapHash == EF.Constant(beatmapHash) &&
                    s.GameMode == EF.Constant(gameMode));

        if (type is LeaderboardType.GlobalWithMods && mods != null)
        {
            scoresGrouped = mods != Mods.None ? scoresGrouped.Where(s => (s.Mods & EF.Constant(mods)) == EF.Constant(mods)) : scoresGrouped.Where(s => s.Mods == EF.Constant(Mods.None));
        }

        if (type is LeaderboardType.Friends && user != null) scoresGrouped = scoresGrouped.Where(s => EF.Constant(user.FriendsList).Contains(s.UserId));
        if (type is LeaderboardType.Country && user != null) scoresGrouped = scoresGrouped.Where(s => s.User.Country == EF.Constant(user.Country));

        var scoresQuery = dbContext.Scores
            .FromSqlRaw(scoresGrouped.SelectUsersPersonalBestScores().ToQueryString());

        var totalCount = await scoresQuery.CountAsync();

        var scores = await scoresQuery
            .OrderByScoreValueDescending()
            .UseQueryOptions(options)
            .ToListAsync();

        return (scores, totalCount);
    }

    public async Task<(List<Score> Scores, int TotalCount)> GetUserScores(int userId, GameMode mode, ScoreTableType type, QueryOptions? options = null)
    {
        var scoresQuery = dbContext.Scores
            .FilterValidScores()
            .Where(s => s.GameMode == EF.Constant(mode));

        switch (type)
        {
            case ScoreTableType.Best:
                scoresQuery = scoresQuery
                    .FilterPassedRankedScores()
                    .SelectUsersPersonalBestScores();
                break;
            case ScoreTableType.Top:
                scoresQuery = scoresQuery
                    .FilterPassedScoreableScores()
                    .SelectBeatmapsBestScores();
                break;
            case ScoreTableType.Recent:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }

        switch (type)
        {
            case ScoreTableType.Best:
                scoresQuery = dbContext.Scores.FromSqlRaw(scoresQuery.ToQueryString())
                    .OrderByDescending(s => s.PerformancePoints)
                    .ThenByDescending(s => s.WhenPlayed);
                break;
            case ScoreTableType.Top:
                scoresQuery = dbContext.Scores.FromSqlRaw(scoresQuery.ToQueryString())
                    .OrderByDescending(s => s.WhenPlayed);
                break;
            case ScoreTableType.Recent:
                scoresQuery = scoresQuery
                    .OrderByDescending(s => s.WhenPlayed);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }

        scoresQuery = scoresQuery.Where(s => s.UserId == userId); // Add where user id query only after forming sqlRaw query, to get proper beatmaps top plays

        var totalCount = await scoresQuery.CountAsync();

        var scores = await scoresQuery
            .UseQueryOptions(options)
            .ToListAsync();

        return (scores, totalCount);
    }

    public async Task<List<Score>> GetScores(GameMode? mode = null, QueryOptions? options = null, int? startFromId = null)
    {
        var scoreQuery = dbContext.Scores.FilterValidScores();

        if (mode != null) scoreQuery = scoreQuery.Where(s => s.GameMode == mode);
        if (startFromId != null) scoreQuery = scoreQuery.Where(s => s.Id >= startFromId);

        return await scoreQuery
            .UseQueryOptions(options)
            .ToListAsync();
    }

    public async Task<long> CountScores()
    {
        return await dbContext.Scores.FilterValidScores().CountAsync();
    }
}