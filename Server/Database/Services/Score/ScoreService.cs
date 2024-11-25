using DatabaseWrapper.Core;
using ExpressionTree;
using osu.Shared;
using Sunrise.Server.Database.Services.Score.Services;
using Sunrise.Server.Helpers;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types;
using Sunrise.Server.Types.Enums;
using Watson.ORM.Sqlite;

namespace Sunrise.Server.Database.Services.Score;

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

    public async Task<List<Models.Score>> GetTopScores(GameMode mode)
    {
        var exp = new Expr("GameMode",
            OperatorEnum.Equals,
            (int)mode).PrependAnd("IsRanked", OperatorEnum.Equals, true).PrependAnd("IsPassed",
            OperatorEnum.Equals,
            true);

        var scores = await _database.SelectManyAsync<Models.Score>(exp,
        [
            new ResultOrder("TotalScore", OrderDirectionEnum.Descending)
        ]);

        // sort by performance points
        return scores.GroupBy(x => x.BeatmapId).Select(x => x.OrderByDescending(y => y.TotalScore).First()).OrderByDescending(x => x.PerformancePoints).ToList();
    }

    public async Task InsertScore(Models.Score score)
    {
        score = await _database.InsertAsync(score);
        await _redis.Set(RedisKey.Score(score.Id), score);
    }

    public async Task<Models.Score?> GetScore(int id)
    {
        var cachedScore = await _redis.Get<Models.Score?>(RedisKey.Score(id));

        if (cachedScore != null) return cachedScore;

        var exp = new Expr("Id", OperatorEnum.Equals, id);
        var score = await _database.SelectFirstAsync<Models.Score?>(exp);

        if (score == null) throw new Exception("Score not found");

        await _redis.Set(RedisKey.Score(id), score);

        return score;
    }

    public async Task<List<Models.Score>> GetUserBestScores(int userId, GameMode mode, int excludeBeatmapId = -1,
        int? limit = null)
    {
        var exp = new Expr("UserId", OperatorEnum.Equals, userId).PrependAnd("GameMode", OperatorEnum.Equals, (int)mode)
            .PrependAnd("BeatmapId", OperatorEnum.NotEquals, excludeBeatmapId)
            .PrependAnd("IsRanked", OperatorEnum.Equals, true).PrependAnd("IsPassed", OperatorEnum.Equals, true);

        var scores = await _database.SelectManyAsync<Models.Score>(exp,
        [
            new ResultOrder("TotalScore", OrderDirectionEnum.Descending)
        ]);

        var bestScores = scores.GroupBy(x => x.BeatmapId).Select(x => x.First()).ToList();

        // I wished I could just add it to the db query
        var rankedScores = bestScores.Where(x => x.BeatmapStatus is BeatmapStatus.Ranked or BeatmapStatus.Approved).ToList();

        return limit == null ? rankedScores : rankedScores.Take(limit.Value).ToList();
    }

    public async Task<Dictionary<int, int>> GetUserMostPlayedMapsIds(int userId, GameMode mode)
    {
        var exp = new Expr("UserId", OperatorEnum.Equals, userId).PrependAnd("GameMode",
            OperatorEnum.Equals,
            (int)mode);

        var scores = await _database.SelectManyAsync<Models.Score>(exp);

        var mostPlayedBeatmap = scores.GroupBy(x => x.BeatmapId).OrderByDescending(x => x.Count())
            .ToDictionary(x => x.Key, x => x.Count());
        return mostPlayedBeatmap;
    }


    public async Task<List<Models.Score>> GetUserScores(int userId, GameMode mode, ScoreTableType type)
    {
        var exp = new Expr("GameMode", OperatorEnum.Equals, (int)mode).PrependAnd("UserId",
            OperatorEnum.Equals,
            userId);

        switch (type)
        {
            case ScoreTableType.Best:
                exp = exp.PrependAnd("IsRanked", OperatorEnum.Equals, true)
                    .PrependAnd("IsPassed", OperatorEnum.Equals, true);
                break;
            case ScoreTableType.Top:
                exp = exp.PrependAnd("IsRanked", OperatorEnum.Equals, true)
                    .PrependAnd("IsPassed", OperatorEnum.Equals, true);
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
                ScoreTableType.Best => new ResultOrder("PerformancePoints", OrderDirectionEnum.Descending),
                ScoreTableType.Recent => new ResultOrder("WhenPlayed", OrderDirectionEnum.Descending),
                ScoreTableType.Top => new ResultOrder("TotalScore", OrderDirectionEnum.Descending),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            }
        ]);

        switch (type)
        {
            case ScoreTableType.Top:
                scores = scores.GroupBy(x => x.BeatmapId).Select(x => x.First()).Where(x => x.UserId == userId)
                    .ToList();
                break;
            case ScoreTableType.Best:
                scores = scores.GroupBy(x => x.BeatmapId).Select(x => x.First()).Where(x => x.BeatmapStatus is BeatmapStatus.Ranked or BeatmapStatus.Approved).ToList();
                break;
        }

        return scores;
    }

    public async Task<long> GetTotalScores()
    {
        var exp = new Expr("Id", OperatorEnum.IsNotNull, null);
        var totalScores = await _database.CountAsync<Models.Score>(exp);

        return totalScores;
    }

    public async Task<IEnumerable<IGrouping<int, Models.Score>>> GetScoresGroupedByBeatmapId()
    {
        var exp = new Expr("Id", OperatorEnum.IsNotNull, null);

        var scores = await _database.SelectManyAsync<Models.Score>(exp);

        return scores.GroupBy(x => x.BeatmapId);
    }

    // TODO: Deprecate? Isnt scores write only????????
    public async Task UpdateScore(Models.Score score)
    {
        score = await _database.UpdateAsync(score);
        await _redis.Set(RedisKey.Score(score.Id), score);
    }

    public async Task<Models.Score?> GetUserLastScore(int userId)
    {
        var exp = new Expr("UserId", OperatorEnum.Equals, userId);

        var scores = await _database.SelectManyAsync<Models.Score>(exp);

        return scores.Count == 0 ? null : scores.OrderBy(x => x.WhenPlayed).ToList().Last();
    }

    public async Task<List<Models.Score>> GetBeatmapScoresById(int beatmapId, GameMode gameMode,
        LeaderboardType type = LeaderboardType.Global, Mods mods = Mods.None, Models.User.User? user = null, bool? modsShouldEqual = true)
    {
        var exp = new Expr("BeatmapId", OperatorEnum.Equals, beatmapId).PrependAnd("GameMode",
            OperatorEnum.Equals,
            (int)gameMode).PrependAnd("IsPassed", OperatorEnum.Equals, true);

        if (modsShouldEqual == true && type is LeaderboardType.GlobalWithMods) exp.PrependAnd("Mods", OperatorEnum.Equals, (int)mods);
        if (type is LeaderboardType.Friends) exp.PrependAnd("UserId", OperatorEnum.In, user?.FriendsList);

        var scores = await _database.SelectManyAsync<Models.Score>(exp);
        scores = scores.GetSortedScoresByScore();

        if (modsShouldEqual == false && type is LeaderboardType.GlobalWithMods) scores = scores.Where(x => x.Mods.HasFlag(mods)).ToList();

        foreach (var score in scores.ToList())
        {
            var scoreUser = await _services.UserService.GetUser(score.UserId);

            if (type == LeaderboardType.Country && scoreUser?.Country != user?.Country ||
                scoreUser?.IsRestricted == true) scores.Remove(score);
        }

        return scores;
    }

    public async Task<int> GetLeaderboardRank(Models.Score score)
    {
        var exp = new Expr("BeatmapHash", OperatorEnum.Equals, score.BeatmapHash).PrependAnd("GameMode",
            OperatorEnum.Equals,
            (int)score.GameMode).PrependAnd("IsPassed", OperatorEnum.Equals, true);
        var scores = await _database.SelectManyAsync<Models.Score>(exp);

        return scores.GetSortedScoresByScore().FindIndex(x => x.Id == score.Id) + 1;
    }

    public async Task<List<Models.Score>> GetBeatmapScores(string beatmapHash, GameMode gameMode,
        LeaderboardType type = LeaderboardType.Global, Mods mods = Mods.None, Models.User.User? user = null)
    {
        var exp = new Expr("BeatmapHash", OperatorEnum.Equals, beatmapHash).PrependAnd("GameMode",
            OperatorEnum.Equals,
            (int)gameMode).PrependAnd("IsPassed", OperatorEnum.Equals, true);

        if (type is LeaderboardType.GlobalWithMods) exp.PrependAnd("Mods", OperatorEnum.Equals, (int)mods);
        if (type is LeaderboardType.Friends) exp.PrependAnd("UserId", OperatorEnum.In, user?.FriendsList);

        var scores = await _database.SelectManyAsync<Models.Score>(exp);
        scores = scores.GetSortedScoresByScore();

        foreach (var score in scores.ToList())
        {
            var scoreUser = await _services.UserService.GetUser(score.UserId);

            if (type == LeaderboardType.Country && scoreUser?.Country != user?.Country ||
                scoreUser?.IsRestricted == true) scores.Remove(score);
        }

        return scores;
    }

    public async Task<List<int>> GetMostPlayedBeatmapsIds(GameMode? gameMode, int page = 1, int limit = 100)
    {
        var exp = new Expr("Id", OperatorEnum.IsNotNull, null);
        if (gameMode != null) exp = exp.PrependAnd("GameMode", OperatorEnum.Equals, (int)gameMode);

        var scores = await _database.SelectManyAsync<Models.Score>(exp);

        var uniqueScores = scores
            .GroupBy(x => x.BeatmapId)
            .OrderByDescending(x => x.Count())
            .Skip((page - 1) * limit)
            .Take(limit);

        return uniqueScores.Select(x => x.Key).ToList();
    }
}