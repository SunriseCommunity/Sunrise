using DatabaseWrapper.Core;
using ExpressionTree;
using Microsoft.Extensions.Logging;
using osu.Shared;
using Sunrise.Shared.Database.Services.Score.Services;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Leaderboards;
using Sunrise.Shared.Extensions.Scores;
using Sunrise.Shared.Objects.Keys;
using Sunrise.Shared.Repositories;
using Watson.ORM.Sqlite;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Database.Services.Score;

public class ScoreService
{
    private readonly WatsonORM _database;
    private readonly ILogger _logger;
    private readonly RedisRepository _redis;
    private readonly DatabaseManager _services;

    public ScoreService(DatabaseManager services, RedisRepository redis, WatsonORM database)
    {
        var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        _logger = loggerFactory.CreateLogger<ScoreService>();

        _services = services;
        _database = database;
        _redis = redis;

        Files = new ScoreFileService(_services, _redis, _database);
    }

    public ScoreFileService Files { get; }

    public async Task<Models.Score> InsertScore(Models.Score score)
    {
        score = await _database.InsertAsync(score);
        await SetCachedScore(score);
        return score;
    }

    public async Task UpdateScore(Models.Score score)
    {
        score = await _database.UpdateAsync(score);
        await SetCachedScore(score);
    }

    public async Task MarkAsDeleted(Models.Score score)
    {
        score.SubmissionStatus = SubmissionStatus.Deleted;
        await UpdateScore(score);
    }

    public async Task<List<Models.Score>> GetBestScoresByGameMode(GameMode mode)
    {
        var restrictedUserIds = await _services.UserService.Moderation.GetRestrictedUsersIds();

        var exp = new Expr("GameMode", OperatorEnum.Equals, (int)mode)
            .PrependAnd("IsScoreable", OperatorEnum.Equals, true)
            .PrependAnd("IsPassed", OperatorEnum.Equals, true)
            .PrependAnd("UserId", OperatorEnum.NotIn, restrictedUserIds)
            .PrependAnd("SubmissionStatus", OperatorEnum.Equals, (int)SubmissionStatus.Best);

        var scores = await _database.SelectManyAsync<Models.Score>(exp);

        return scores.GetScoresGroupedByUsersBest().SortScoresByPerformancePoints();
    }

    public async Task<Models.Score?> GetScore(int id)
    {
        var cachedScore = await _redis.Get<Models.Score?>(RedisKey.ScoreById(id));
        if (cachedScore != null) return cachedScore;

        var exp = new Expr("Id", OperatorEnum.Equals, id)
            .PrependAnd("SubmissionStatus", OperatorEnum.NotEquals, (int)SubmissionStatus.Deleted);
        var score = await _database.SelectFirstAsync<Models.Score?>(exp);
        if (score == null) return null;

        await SetCachedScore(score);

        return score;
    }

    public async Task<Models.Score?> GetScore(string scoreHash)
    {
        var cachedScoreId = await _redis.Get<Models.Score?>(RedisKey.ScoreIdByScoreHash(scoreHash));

        if (cachedScoreId != null)
        {
            var cachedScore = await GetScore(cachedScoreId.Id);
            if (cachedScore != null) return cachedScore;
        }

        var exp = new Expr("ScoreHash", OperatorEnum.Equals, scoreHash).PrependAnd("SubmissionStatus", OperatorEnum.NotEquals, (int)SubmissionStatus.Deleted);
        var score = await _database.SelectFirstAsync<Models.Score?>(exp);
        if (score == null) return null;

        await SetCachedScore(score);

        return score;
    }

    public async Task SetCachedScore(Models.Score score)
    {
        await _redis.Set(RedisKey.ScoreById(score.Id), score);
        await _redis.Set(RedisKey.ScoreIdByScoreHash(score.ScoreHash), score);
    }

    public async Task<Dictionary<int, int>> GetUserMostPlayedBeatmapsIds(int userId, GameMode mode)
    {
        var user = await _services.UserService.GetUser(userId);
        if (user.IsRestricted()) return [];

        var exp = new Expr("UserId", OperatorEnum.Equals, userId)
            .PrependAnd("GameMode", OperatorEnum.Equals, (int)mode)
            .PrependAnd("SubmissionStatus", OperatorEnum.NotEquals, (int)SubmissionStatus.Deleted);

        var scores = await _database.SelectManyAsync<Models.Score>(exp);
        return scores
            .GroupScoresByBeatmapId()
            .OrderByDescending(x => x.Count())
            .ToDictionary(x => x.Key, x => x.Count());
    }

    public async Task<Models.Score?> GetUserLastScore(int userId)
    {
        var exp = new Expr("UserId", OperatorEnum.Equals, userId).PrependAnd("SubmissionStatus", OperatorEnum.NotEquals, (int)SubmissionStatus.Deleted);

        var score = await _database.SelectFirstAsync<Models.Score>(exp,
        [
            new ResultOrder("WhenPlayed", OrderDirectionEnum.Descending)
        ]);

        return score;
    }

    public async Task<List<Models.Score>> GetBeatmapScores(string beatmapHash, GameMode gameMode,
        LeaderboardType type = LeaderboardType.Global, Mods? mods = null, Models.User.User? user = null)
    {
        var restrictedUserIds = await _services.UserService.Moderation.GetRestrictedUsersIds();

        var exp = new Expr("BeatmapHash", OperatorEnum.Equals, beatmapHash)
            .PrependAnd("GameMode", OperatorEnum.Equals, (int)gameMode)
            .PrependAnd("IsPassed", OperatorEnum.Equals, true)
            .PrependAnd("IsScoreable", OperatorEnum.Equals, true)
            .PrependAnd("UserId", OperatorEnum.NotIn, restrictedUserIds)
            .PrependAnd("SubmissionStatus", OperatorEnum.Equals, (int)SubmissionStatus.Best);

        if (type is LeaderboardType.GlobalWithMods && mods != null) exp.PrependAnd("Mods", OperatorEnum.Equals, (int)mods);
        if (type is LeaderboardType.Friends) exp.PrependAnd("UserId", OperatorEnum.In, user?.FriendsList);

        var scores = await _database.SelectManyAsync<Models.Score>(exp);
        scores = scores.GetScoresGroupedByUsersBest();

        if (type == LeaderboardType.Country)
        {
            foreach (var score in scores.ToList())
            {
                // Should be fine, while we have users in cache.
                var scoreUser = await _services.UserService.GetUser(score.UserId);

                if (scoreUser?.Country != user?.Country) scores.Remove(score);
            }
        }

        return scores.SortScoresByTheirScoreValue();
    }

    public async Task<List<Models.Score>> GetUserScores(int userId, GameMode mode, ScoreTableType type)
    {
        var user = await _services.UserService.GetUser(userId);
        if (user == null) return [];

        if (user.IsRestricted()) return [];

        var exp = new Expr("UserId", OperatorEnum.Equals, userId)
            .PrependAnd("GameMode", OperatorEnum.Equals, (int)mode)
            .PrependAnd("SubmissionStatus", OperatorEnum.NotEquals, (int)SubmissionStatus.Deleted);

        switch (type)
        {
            case ScoreTableType.Best:
                exp = exp.PrependAnd("IsScoreable", OperatorEnum.Equals, true)
                    .PrependAnd("IsPassed", OperatorEnum.Equals, true)
                    .PrependAnd("BeatmapStatus", OperatorEnum.NotEquals, (int)BeatmapStatus.Qualified)
                    .PrependAnd("BeatmapStatus", OperatorEnum.NotEquals, (int)BeatmapStatus.Loved)
                    .PrependAnd("SubmissionStatus", OperatorEnum.Equals, (int)SubmissionStatus.Best);
                break;
            case ScoreTableType.Top:
                exp = exp.PrependAnd("IsScoreable", OperatorEnum.Equals, true)
                    .PrependAnd("IsPassed", OperatorEnum.Equals, true)
                    .PrependAnd("SubmissionStatus", OperatorEnum.Equals, (int)SubmissionStatus.Best);
                break;
            case ScoreTableType.Recent:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }

        var scores = await _database.SelectManyAsync<Models.Score>(exp,
        [
            type switch
            {

                ScoreTableType.Recent => new ResultOrder("WhenPlayed", OrderDirectionEnum.Descending),
                ScoreTableType.Best => new ResultOrder("TotalScore", OrderDirectionEnum.Descending),
                ScoreTableType.Top => new ResultOrder("WhenPlayed", OrderDirectionEnum.Descending),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            }
        ]);

        switch (type)
        {
            case ScoreTableType.Top:
                scores = scores.GetScoresGroupedByBeatmapBest().Where(x => x.UserId == userId).ToList();
                break;
            case ScoreTableType.Best:
                scores = scores.GetScoresGroupedByUsersBest().SortScoresByPerformancePoints();
                break;
        }

        return scores;
    }

    public async Task<long> GetTotalScores()
    {
        var exp = new Expr("Id", OperatorEnum.IsNotNull, null).PrependAnd("SubmissionStatus", OperatorEnum.NotEquals, (int)SubmissionStatus.Deleted);
        var totalScores = await _database.CountAsync<Models.Score>(exp);

        return totalScores;
    }

    public async Task<List<Models.Score>> GetAllScores(GameMode? mode = null)
    {
        var exp = new Expr("Id", OperatorEnum.IsNotNull, null).PrependAnd("SubmissionStatus", OperatorEnum.NotEquals, (int)SubmissionStatus.Deleted);
        if (mode != null) exp.PrependAnd("GameMode", OperatorEnum.Equals, (int)mode);

        return await _database.SelectManyAsync<Models.Score>(exp);
    }
}