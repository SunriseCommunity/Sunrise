using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using osu.Shared;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Database.Services;
using Sunrise.Shared.Enums.Leaderboards;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Utils;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Database.Repositories;

public class ScoreRepository
{
    private readonly DatabaseService _databaseService;
    private readonly SunriseDbContext _dbContext;

    public ScoreRepository(DatabaseService databaseService)
    {
        _databaseService = databaseService;
        _dbContext = databaseService.DbContext;

        Files = new ScoreFileService(_databaseService);
    }

    public ScoreFileService Files { get; }

    public async Task<Result> AddScore(Score score)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            _dbContext.Scores.Add(score);
            await _dbContext.SaveChangesAsync();
        });
    }

    public async Task<Result> UpdateScore(Score score)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            _dbContext.UpdateEntity(score);
            await _dbContext.SaveChangesAsync();
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
        var isModeWithoutScoreMultiplier = mode.IsGameModeWithoutScoreMultiplier();

        var groupedBestScores = _dbContext.Scores
            .GroupBy(x => x.BeatmapId)
            .Select(g =>
                g.OrderByDescending(x => isModeWithoutScoreMultiplier ? x.PerformancePoints : x.TotalScore
                ).First());

        var queryScore = _dbContext.Scores
            .FromSqlRaw(groupedBestScores.ToQueryString())
            .OrderByDescending(x => x.PerformancePoints)
            .ThenByDescending(x => x.WhenPlayed)
            .FilterValidScores()
            .FilterPassedRankedScores()
            .Where(x => x.GameMode == mode && x.IsScoreable && x.IsPassed && x.SubmissionStatus == SubmissionStatus.Best)
            .UseQueryOptions(options);

        var queryResult = await queryScore.ToListAsync();

        return queryResult;
    }

    public async Task<Score?> GetScore(int id, QueryOptions? options = null)
    {
        return await _dbContext.Scores
            .FilterValidScores()
            .Where(s => s.Id == id)
            .UseQueryOptions(options)
            .FirstOrDefaultAsync();
    }

    public async Task<Score?> GetScore(string scoreHash, QueryOptions? options = null)
    {
        return await _dbContext.Scores
            .FilterValidScores()
            .Where(s => s.ScoreHash == scoreHash)
            .UseQueryOptions(options)
            .FirstOrDefaultAsync();
    }

    public async Task<(List<KeyValuePair<int, int>>, int)> GetUserMostPlayedBeatmapIds(int userId, GameMode mode, QueryOptions? options = null)
    {
        var groupedBeatmapsQuery = _dbContext.Scores
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
        return await _dbContext.Scores
            .FilterValidScores()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.WhenPlayed)
            .UseQueryOptions(options)
            .FirstOrDefaultAsync();
    }

    public async Task<(List<Score> Scores, int TotalCount)> GetBeatmapScores(string beatmapHash, GameMode gameMode,
        LeaderboardType type = LeaderboardType.Global, Mods? mods = null, User? user = null, QueryOptions? options = null)
    {
        var scoresGrouped = _dbContext.Scores
            .FilterValidScores()
            .Where(
                s =>
                    s.BeatmapHash == EF.Constant(beatmapHash) &&
                    s.GameMode == EF.Constant(gameMode) &&
                    s.IsPassed &&
                    s.IsScoreable);
        
        if (type is LeaderboardType.GlobalWithMods && mods != null) scoresGrouped = scoresGrouped.Where(s => s.Mods == EF.Constant(mods));
        if (type is LeaderboardType.Friends && user != null) scoresGrouped = scoresGrouped.Where(s => EF.Constant(user.FriendsList).Contains(s.UserId));
        if (type is LeaderboardType.Country && user != null) scoresGrouped = scoresGrouped.Where(s => s.User.Country == EF.Constant(user.Country));

        var scoresQuery = _dbContext.Scores
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
        var scoresQuery = _dbContext.Scores
            .FilterValidScores()
            .Where(s => s.GameMode == EF.Constant(mode));

        switch (type)
        {
            case ScoreTableType.Best:
                scoresQuery = scoresQuery
                    .FilterPassedRankedScores()
                    .Where(s => s.SubmissionStatus == SubmissionStatus.Best)
                    .SelectUsersPersonalBestScores();

                break;
            case ScoreTableType.Top:
                scoresQuery = scoresQuery
                    .FilterPassedScoreableScores()
                    .Where(s => s.SubmissionStatus == SubmissionStatus.Best)
                    .SelectBeatmapsBestScores();
                break;
            case ScoreTableType.Recent:
                scoresQuery = scoresQuery
                    .OrderByDescending(s => s.WhenPlayed);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }

        switch (type)
        {
            case ScoreTableType.Best:
                scoresQuery = _dbContext.Scores.FromSqlRaw(scoresQuery.ToQueryString())
                    .OrderByDescending(s => s.PerformancePoints)
                    .ThenByDescending(s => s.WhenPlayed);
                break;
            case ScoreTableType.Top:
                scoresQuery = _dbContext.Scores.FromSqlRaw(scoresQuery.ToQueryString())
                    .OrderByDescending(s => s.WhenPlayed);
                break;
            case ScoreTableType.Recent:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }

        scoresQuery = scoresQuery.Where(s => s.UserId == userId);

        var totalCount = await scoresQuery.CountAsync();

        var scores = await scoresQuery
            .UseQueryOptions(options)
            .ToListAsync();

        return (scores, totalCount);
    }

    public async Task<List<Score>> GetScores(GameMode? mode = null, QueryOptions? options = null)
    {
        var scoreQuery = _dbContext.Scores.FilterValidScores();

        if (mode != null) scoreQuery = scoreQuery.Where(s => s.GameMode == mode);

        return await scoreQuery
            .UseQueryOptions(options)
            .ToListAsync();
    }

    public async Task<long> CountScores()
    {
        return await _dbContext.Scores.FilterValidScores().CountAsync();
    }
}