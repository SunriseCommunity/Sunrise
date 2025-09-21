using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using osu.Shared;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Database.Services;
using Sunrise.Shared.Database.Services.Users;
using Sunrise.Shared.Enums.Leaderboards;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Utils;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Database.Repositories;

public class ScoreRepository(ILogger<ScoreRepository> logger, SunriseDbContext dbContext, ScoreFileService scoreFileService, UserRelationshipService userRelationshipService)
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

    public async Task<(List<Score>, int)> GetBestScoresByGameMode(GameMode mode, QueryOptions? options = null, CancellationToken ct = default)
    {
        var groupedBestScores = dbContext.Scores
            .FilterValidScores()
            .FilterPassedRankedScores()
            .Where(x => x.GameMode == EF.Constant(mode))
            .SelectUsersPersonalBestScores(Configuration.UseNewPerformanceCalculationAlgorithm);

        var scoresQuery = dbContext.Scores
            .FromSqlRaw(groupedBestScores.ToQueryString())
            .OrderByDescending(x => x.PerformancePoints)
            .ThenByDescending(x => x.WhenPlayed);

        var totalCount = options?.IgnoreCountQueryIfExists == true ? -1 : await scoresQuery.CountAsync(cancellationToken: ct);

        var scores = await scoresQuery.UseQueryOptions(options).ToListAsync(cancellationToken: ct);

        return (scores, totalCount);
    }

    public async Task<Score?> GetScore(int id, QueryOptions? options = null, CancellationToken ct = default)
    {
        return await dbContext.Scores
            .FilterValidScores()
            .Where(s => s.Id == id)
            .UseQueryOptions(options)
            .FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<Score?> GetScore(string scoreHash, QueryOptions? options = null, CancellationToken ct = default)
    {
        return await dbContext.Scores
            .FilterValidScores()
            .Where(s => s.ScoreHash == scoreHash)
            .UseQueryOptions(options)
            .FirstOrDefaultAsync(cancellationToken: ct);
    }


    public async Task<(List<KeyValuePair<int, int>>, int)> GetUserMostPlayedBeatmapIds(int userId, GameMode mode, QueryOptions? options = null, CancellationToken ct = default)
    {
        var groupedBeatmapsQuery = dbContext.Scores
            .FilterValidScores()
            .Where(s => s.UserId == userId && s.GameMode == mode)
            .GroupScoresByBeatmapPlaycount();

        var groupedBeatmapsCount = options?.IgnoreCountQueryIfExists == true ? -1 : await groupedBeatmapsQuery.CountAsync(cancellationToken: ct);

        var mostPlayedBeatmaps = await groupedBeatmapsQuery
            .OrderByDescending(g => g.Count)
            .ThenByDescending(g => g.WhenPlayed)
            .UseQueryOptions(options)
            .Select(g => new KeyValuePair<int, int>(g.Key, g.Count))
            .ToListAsync(cancellationToken: ct);

        return (mostPlayedBeatmaps, groupedBeatmapsCount);
    }

    public async Task<Score?> GetUserLastScore(int userId, QueryOptions? options = null, CancellationToken ct = default)
    {
        return await dbContext.Scores
            .FilterValidScores()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.WhenPlayed)
            .UseQueryOptions(options)
            .FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<(List<Score> Scores, int TotalCount)> GetBeatmapScores(string beatmapHash, GameMode gameMode,
        LeaderboardType type = LeaderboardType.Global, Mods? mods = null, User? user = null, QueryOptions? options = null, CancellationToken ct = default)
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
            scoresGrouped = scoresGrouped.Where(s => s.Mods == EF.Constant(mods));
        }

        if (type is LeaderboardType.GlobalIncludesMods && mods != null)
        {
            scoresGrouped = mods != Mods.None ? scoresGrouped.Where(s => (s.Mods & EF.Constant(mods)) == EF.Constant(mods)) : scoresGrouped.Where(s => s.Mods == EF.Constant(Mods.None));
        }

        if (type is LeaderboardType.Country && user != null) scoresGrouped = scoresGrouped.Where(s => s.User.Country == EF.Constant(user.Country));

        if (type is LeaderboardType.Friends && user != null)
        {
            var (friends, _) = await userRelationshipService.GetUserFriends(user.Id,
                new QueryOptions
                {
                    IgnoreCountQueryIfExists = true
                },
                ct);

            var friendIds = friends.Select(f => f.Id).ToHashSet();

            scoresGrouped = scoresGrouped.Where(s => friendIds.Contains(s.UserId));
        }

        var scoresQuery = dbContext.Scores
            .FromSqlRaw(scoresGrouped.SelectUsersPersonalBestScores().ToQueryString());

        var totalCount = options?.IgnoreCountQueryIfExists == true ? -1 : await scoresQuery.CountAsync(cancellationToken: ct);

        var scores = await scoresQuery
            .OrderByScoreValueDescending()
            .UseQueryOptions(options)
            .ToListAsync(cancellationToken: ct);

        return (scores, totalCount);
    }

    public async Task<(List<Score> Scores, int TotalCount)> GetUserScores(int userId, GameMode mode, ScoreTableType type, QueryOptions? options = null, CancellationToken ct = default)
    {
        var scoresQuery = dbContext.Scores
            .FilterValidScores()
            .Where(s => s.GameMode == EF.Constant(mode));

        switch (type)
        {
            case ScoreTableType.Best:
                scoresQuery = scoresQuery
                    .FilterPassedRankedScores()
                    .SelectUsersPersonalBestScores(Configuration.UseNewPerformanceCalculationAlgorithm);
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

        scoresQuery = scoresQuery.Where(s => s.UserId == userId); // We are adding user id query only after forming sqlRaw query to get proper beatmaps top plays

        var totalCount = options?.IgnoreCountQueryIfExists == true ? -1 : await scoresQuery.CountAsync(cancellationToken: ct);

        var scores = await scoresQuery
            .UseQueryOptions(options)
            .ToListAsync(cancellationToken: ct);

        return (scores, totalCount);
    }

    public async Task<(List<Score>, int)> GetScores(GameMode? mode = null, QueryOptions? options = null, int? startFromId = null, CancellationToken ct = default)
    {
        var scoresQuery = dbContext.Scores.FilterValidScores();

        if (mode != null) scoresQuery = scoresQuery.Where(s => s.GameMode == mode);
        if (startFromId != null) scoresQuery = scoresQuery.Where(s => s.Id >= startFromId);

        var totalCount = options?.IgnoreCountQueryIfExists == true ? -1 : await scoresQuery.CountAsync(cancellationToken: ct);

        var scores = await scoresQuery
            .UseQueryOptions(options)
            .ToListAsync(cancellationToken: ct);

        return (scores, totalCount);
    }

    public async Task<List<Score>> EnrichScoresWithLeaderboardPosition(List<Score> scores, CancellationToken ct = default)
    {
        if (scores.Count == 0) return scores;

        var scoresIds = string.Join(",", scores.Select(s => s.Id));

        await using var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync(ct);

        var gameModesWithoutScoreMultiplier = GameModeExtensions.GetGameModesWithoutScoreMultiplier();

        var orderByValue = gameModesWithoutScoreMultiplier.Contains(scores.FirstOrDefault()?.GameMode ?? GameMode.Standard) ? nameof(Score.PerformancePoints) : nameof(Score.TotalScore);

        var command = connection.CreateCommand();
        command.CommandText = $"""
                               
                                       SELECT Id,
                                              RANK() OVER (PARTITION BY BeatmapId ORDER BY {orderByValue} DESC) AS LeaderboardPosition
                                       FROM score
                                       WHERE Id IN ({scoresIds})
                               """;

        var leaderboardMap = new Dictionary<long, int>();

        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var id = reader.GetInt64(0);
                var rank = reader.GetInt32(1);
                leaderboardMap[id] = rank;
            }
        }

        foreach (var score in scores)
        {
            if (leaderboardMap.TryGetValue(score.Id, out var position))
            {
                score.LocalProperties.LeaderboardPosition = position;
            }
        }

        return scores;
    }

    public async Task<long> CountScores(CancellationToken ct = default)
    {
        return await dbContext.Scores.FilterValidScores().CountAsync(cancellationToken: ct);
    }
}