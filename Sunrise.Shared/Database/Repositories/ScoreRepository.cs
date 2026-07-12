using System.Data;
using CSharpFunctionalExtensions;
using EntityFrameworkCore.Locking;
using Microsoft.EntityFrameworkCore;
using osu.Shared;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Database.Services;
using Sunrise.Shared.Database.Services.Users;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Leaderboards;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Extensions.Scores;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Utils;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;

namespace Sunrise.Shared.Database.Repositories;

public class ScoreRepository(SunriseDbContext dbContext, ScoreFileService scoreFileService, UserRelationshipService userRelationshipService)
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

    public async Task<Score?> GetScore(int id, QueryOptions? options = null, bool? filterValidScores = true, CancellationToken ct = default)
    {
        var baseScores = dbContext.Scores.AsQueryable();

        if (filterValidScores.HasValue && filterValidScores.Value)
        {
            baseScores = baseScores.FilterValidScores();
        }

        return await baseScores
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
            .Where(s =>
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

        if (type is LeaderboardType.Country && user != null) scoresGrouped = scoresGrouped.Where(s => s.User!.Country == EF.Constant(user.Country));

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

    public async Task<Dictionary<DateTime, int>> GetUserPlayHistoryScores(int userId, CancellationToken ct = default)
    {
        return await dbContext.Scores
            .FilterValidScores()
            .Where(s => s.UserId == userId)
            .GroupBy(s => new
            {
                s.WhenPlayed.Year,
                s.WhenPlayed.Month
            })
            .Select(g => new
            {
                Date = new DateTime(g.Key.Year, g.Key.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                Count = g.Count()
            })
            .ToDictionaryAsync(x => x.Date, x => x.Count, ct);
    }

    public async Task<(List<Score>, int)> GetScores(
        GameMode? mode = null,
        QueryOptions? options = null,
        int? startFromId = null,
        int? userId = null,
        Mods? mods = null,
        SubmissionStatus? submissionStatus = null,
        BeatmapStatus? beatmapStatus = null,
        DateTime? submittedFrom = null,
        DateTime? submittedTo = null,
        ScoreSortType? sort = null,
        bool filterValidScores = true,
        CancellationToken ct = default)
    {
        var scoresQuery = BuildScoresQuery(mode,
            startFromId,
            userId,
            mods,
            submissionStatus,
            beatmapStatus,
            submittedFrom,
            submittedTo,
            filterValidScores);

        scoresQuery = sort switch
        {
            ScoreSortType.Performance => scoresQuery.OrderByDescending(s => s.PerformancePoints).ThenByDescending(s => s.WhenPlayed),
            ScoreSortType.Date => scoresQuery.OrderByDescending(s => s.WhenPlayed),
            _ => scoresQuery
        };

        var totalCount = options?.IgnoreCountQueryIfExists == true ? -1 : await scoresQuery.CountAsync(cancellationToken: ct);

        var scores = await scoresQuery
            .UseQueryOptions(options)
            .ToListAsync(cancellationToken: ct);

        return (scores, totalCount);
    }

    public async Task<List<Score>> GetScoresForBulkProcessing(
        GameMode? mode = null,
        int? userId = null,
        Mods? mods = null,
        SubmissionStatus? submissionStatus = null,
        BeatmapStatus? beatmapStatus = null,
        DateTime? submittedFrom = null,
        DateTime? submittedTo = null,
        int? startFromId = null,
        int limit = 100,
        CancellationToken ct = default)
    {
        var scoresQuery = BuildScoresQuery(mode,
            startFromId,
            userId,
            mods,
            submissionStatus,
            beatmapStatus,
            submittedFrom,
            submittedTo,
            false);

        return await scoresQuery
            .OrderBy(s => s.Id)
            .Take(limit)
            .ToListAsync(ct);
    }

    private IQueryable<Score> BuildScoresQuery(
        GameMode? mode,
        int? startFromId,
        int? userId,
        Mods? mods,
        SubmissionStatus? submissionStatus,
        BeatmapStatus? beatmapStatus,
        DateTime? submittedFrom,
        DateTime? submittedTo,
        bool filterValidScores)
    {
        var scoresQuery = filterValidScores ? dbContext.Scores.FilterValidScores() : dbContext.Scores.AsQueryable();

        if (mode != null) scoresQuery = scoresQuery.Where(s => s.GameMode == mode);
        if (startFromId != null) scoresQuery = scoresQuery.Where(s => s.Id >= startFromId);
        if (userId != null) scoresQuery = scoresQuery.Where(s => s.UserId == userId);
        if (submissionStatus != null) scoresQuery = scoresQuery.Where(s => s.SubmissionStatus == submissionStatus);
        if (beatmapStatus != null) scoresQuery = scoresQuery.Where(s => s.BeatmapStatus == beatmapStatus);
        if (submittedFrom != null) scoresQuery = scoresQuery.Where(s => s.WhenPlayed >= submittedFrom);
        if (submittedTo != null) scoresQuery = scoresQuery.Where(s => s.WhenPlayed <= submittedTo);
        if (mods != null) scoresQuery = scoresQuery.Where(s => s.Mods == EF.Constant(mods.Value));

        return scoresQuery;
    }

    public async Task<List<Score>> EnrichScoresWithLeaderboardPosition(List<Score> scores, CancellationToken ct = default)
    {
        if (scores.Count == 0) return scores;

        var scoresIds = string.Join(",", scores.Select(s => s.Id));

        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(ct);

        var gameModesWithoutScoreMultiplier = GameModeExtensions.GetGameModesWithoutScoreMultiplier();

        var orderByValue = gameModesWithoutScoreMultiplier.Contains(scores.FirstOrDefault()?.GameMode ?? GameMode.Standard) ? nameof(Score.PerformancePoints) : nameof(Score.TotalScore);

        await using var command = connection.CreateCommand();
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

    public async Task<Dictionary<GameMode, long>> CountScoresByGameMode(CancellationToken ct = default)
    {
        return await dbContext.Scores
            .FilterValidScores()
            .GroupBy(s => s.GameMode)
            .Select(g => new
            {
                GameMode = g.Key,
                Count = g.LongCount()
            })
            .ToDictionaryAsync(k => k.GameMode, v => v.Count, ct);
    }

    public async Task<(Score? Score, UserBeatmapPeers Peers)> GetUserScoreByIdWithBeatmapPeersForUpdate(
        int userId,
        string beatmapHash,
        GameMode gameMode,
        Mods mods,
        int? scoreId = null,
        CancellationToken ct = default)
    {
        var validPeersQuery = dbContext.Scores
            .AsNoTracking()
            .Where(s =>
                s.UserId == userId
                && s.BeatmapHash == beatmapHash
                && s.GameMode == gameMode)
            .FilterValidScores()
            .FilterPassedScoreableScores();

        if (scoreId.HasValue)
            validPeersQuery = validPeersQuery.Where(s => s.Id != scoreId.Value);

        var validPeers = await validPeersQuery.ToListAsync(ct);

        var idsToLock = GetUserPersonalBestScoreIds(validPeers, userId, mods);
        if (scoreId.HasValue)
            idsToLock.Add(scoreId.Value);

        if (idsToLock.Count == 0)
            return (null, new UserBeatmapPeers(null, null));

        var lockedScores = await dbContext.Scores
            .Where(s => idsToLock.Contains(s.Id))
            .OrderBy(s => s.Id)
            .ForUpdate()
            .ToListAsync(ct);

        foreach (var score in lockedScores)
        {
            score.LocalProperties = score.LocalProperties.FromScore(score);
        }

        var targetScore = scoreId.HasValue ? lockedScores.SingleOrDefault(s => s.Id == scoreId.Value) : null;
        var lockedPeers = lockedScores.Where(s => s.Id != scoreId).ToList();

        var peers = new UserBeatmapPeers(
            lockedPeers.Where(s => s.Mods == mods).ToList().GetUserPersonalBestScores(userId),
            lockedPeers.GetUserPersonalBestScores(userId));

        return (targetScore, peers);
    }

    private static List<int> GetUserPersonalBestScoreIds(List<Score> peers, int userId, Mods mods)
    {
        var sameModsBest = peers.Where(s => s.Mods == mods).ToList().GetUserPersonalBestScores(userId);
        var overallBest = peers.GetUserPersonalBestScores(userId);

        return new List<Score?>
            {
                sameModsBest?.BestScoreByScoreValue,
                sameModsBest?.BestScoreForPerformanceCalculation,
                overallBest?.BestScoreByScoreValue,
                overallBest?.BestScoreForPerformanceCalculation
            }
            .Where(s => s != null)
            .Select(s => s!.Id)
            .Distinct()
            .ToList();
    }

    public async Task<int?> GetUserMaxComboExcluding(
        int userId,
        GameMode gameMode,
        int? excludeScoreId = null,
        CancellationToken ct = default)
    {
        var query = dbContext.Scores
            .AsNoTracking()
            .FilterValidScores()
            .FilterPassedScoreableScores()
            .Where(s => s.UserId == userId && s.GameMode == gameMode);

        if (excludeScoreId.HasValue)
        {
            var excludeId = excludeScoreId.Value;
            query = query.Where(s => s.Id != excludeId);
        }

        var hasAny = await query.AnyAsync(ct);
        if (!hasAny)
            return null;

        return await query.MaxAsync(s => (int?)s.MaxCombo, ct);
    }

    public async Task<int?> GetUserIdByScoreId(int scoreId, CancellationToken ct = default)
    {
        return await dbContext.Scores
            .Where(p => p.Id == scoreId)
            .Select(p => (int?)p.UserId)
            .FirstOrDefaultAsync(ct);
    }
}