using ExpressionTree;
using Sunrise.Server.Database.Models.User;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types;
using Watson.ORM.Sqlite;
using GameMode = Sunrise.Server.Types.Enums.GameMode;

namespace Sunrise.Server.Database.Services.User.Services;

public class UserStatsSnapshotService
{
    private readonly WatsonORM _database;
    private readonly ILogger _logger;
    private readonly RedisRepository _redis;

    public UserStatsSnapshotService(RedisRepository redis, WatsonORM database)
    {
        var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        _logger = loggerFactory.CreateLogger<UserStatsSnapshotService>();

        _database = database;
        _redis = redis;
    }

    public async Task<UserStatsSnapshot> GetUserStatsSnapshot(int userId, GameMode mode)
    {
        var cachedSnapshot = await _redis.Get<UserStatsSnapshot>(RedisKey.UserStatsSnapshot(userId, mode));
        if (cachedSnapshot != null) return cachedSnapshot;

        var exp = new Expr("UserId", OperatorEnum.Equals, userId).PrependAnd("GameMode",
            OperatorEnum.Equals,
            (int)mode);
        var snapshot = await _database.SelectFirstAsync<UserStatsSnapshot?>(exp);

        if (snapshot == null)
        {
            snapshot = new UserStatsSnapshot
            {
                UserId = userId,
                GameMode = mode
            };
            snapshot = await InsertUserStatsSnapshot(snapshot);
        }

        await _redis.Set(RedisKey.UserStatsSnapshot(userId, mode), snapshot);

        return snapshot;
    }

    public async Task UpdateUserStatsSnapshot(UserStatsSnapshot snapshot)
    {
        snapshot = await _database.UpdateAsync(snapshot);
        await _redis.Set(RedisKey.UserStatsSnapshot(snapshot.UserId, snapshot.GameMode), snapshot);
    }

    public async Task DeleteUserStatsSnapshot(int userId)
    {
        var exp = new Expr("UserId", OperatorEnum.Equals, userId);
        var snapshot = await _database.SelectFirstAsync<UserStatsSnapshot?>(exp);
        if (snapshot == null) return;

        await _redis.Remove(RedisKey.UserStatsSnapshot(snapshot.UserId, snapshot.GameMode));
    }

    public async Task<UserStatsSnapshot> InsertUserStatsSnapshot(UserStatsSnapshot snapshot)
    {
        snapshot = await _database.InsertAsync(snapshot);
        await _redis.Set(RedisKey.UserStatsSnapshot(snapshot.UserId, snapshot.GameMode), snapshot);
        return snapshot;
    }
}