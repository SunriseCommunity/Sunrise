using ExpressionTree;
using osu.Shared;
using Sunrise.Server.Application;
using Sunrise.Server.Database.Models.User;
using Sunrise.Server.Database.Services.User.Services;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types;
using Watson.ORM.Sqlite;

namespace Sunrise.Server.Database.Services.User;

public class UserService
{
    private readonly WatsonORM _database;
    private readonly ILogger _logger;
    private readonly RedisRepository _redis;
    private readonly DatabaseManager _services;

    public UserService(DatabaseManager services, RedisRepository redis, WatsonORM database)
    {
        var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        _logger = loggerFactory.CreateLogger<UserService>();

        _database = database;
        _redis = redis;
        _services = services;

        Stats = new UserStatsService(_services, _redis, _database);
        Favourites = new UserFavouritesService(_services, _redis, _database);
        Medals = new UserMedalsService(_services, _redis, _database);
        Moderation = new UserModerationService(_services, _redis, _database);
        Files = new UserFileService(_services, _redis, _database);
    }

    public UserStatsService Stats { get; }
    public UserFavouritesService Favourites { get; }
    public UserMedalsService Medals { get; }
    public UserModerationService Moderation { get; }
    public UserFileService Files { get; }

    public async Task<Models.User.User> InsertUser(Models.User.User user)
    {
        user = await _database.InsertAsync(user);

        var modes = Enum.GetValues<GameMode>();

        foreach (var mode in modes)
        {
            var stats = new UserStats
            {
                UserId = user.Id,
                GameMode = mode
            };
            await Stats.InsertUserStats(stats);
        }

        await _redis.Set(RedisKey.UserById(user.Id), user);

        return user;
    }

    public async Task<Models.User.User?> GetUser(int? id = null, string? username = null, string? email = null,
        string? passhash = null, bool useCache = true)
    {
        var redisKeys = new List<string>
        {
            RedisKey.UserById(id ?? 0),
            RedisKey.UserByEmail(email ?? "")
        };

        if (username != null)
        {
            if (passhash != null)
                redisKeys.Add(RedisKey.UserByUsernameAndPassHash(username, passhash));
            else
                redisKeys.Add(RedisKey.UserByUsername(username));
        }

        var cachedUser = await _redis.Get<Models.User.User?>([.. redisKeys]);

        if (cachedUser != null && useCache) return cachedUser;

        if (passhash != null && id == null && username == null && email == null)
            throw new Exception("Passhash provided without any other parameters");

        var exp = new Expr("Id", OperatorEnum.IsNotNull, null);
        if (id != null) exp = exp.PrependAnd("Id", OperatorEnum.Equals, id);
        if (username != null) exp = exp.PrependAnd("Username", OperatorEnum.Equals, username);
        if (email != null) exp = exp.PrependAnd("Email", OperatorEnum.Equals, email);
        if (passhash != null) exp = exp.PrependAnd("Passhash", OperatorEnum.Equals, passhash);

        var user = await _database.SelectFirstAsync<Models.User.User?>(exp);

        if (user == null) return null;

        await _redis.Set(
            [RedisKey.UserById(user.Id), RedisKey.UserByUsername(user.Username), RedisKey.UserByEmail(user.Email)],
            user);

        return user;
    }

    public async Task UpdateUser(Models.User.User user)
    {
        await _database.UpdateAsync(user);

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        var session = sessions.GetSession(userId: user.Id);

        if (session != null)
            await session.UpdateUser(user);

        await _redis.Set(
            [
                RedisKey.UserById(user.Id), RedisKey.UserByUsername(user.Username), RedisKey.UserByEmail(user.Email),
                RedisKey.UserByUsernameAndPassHash(user.Username, user.Passhash)
            ],
            user);
    }

    // Note: Unsafe cache?
    public async Task<List<Models.User.User>?> GetAllUsers(bool useCache = true)
    {
        var cachedStats = await _redis.Get<List<Models.User.User>>(RedisKey.AllUsers());

        if (cachedStats != null && useCache) return cachedStats;

        var users = await _database.SelectManyAsync<Models.User.User>(new Expr("Id", OperatorEnum.IsNotNull, null).PrependAnd("IsRestricted", OperatorEnum.Equals, false));

        if (users == null) return null;

        await _redis.Set(RedisKey.AllUsers(), users);

        return users;
    }

    public async Task<List<Models.User.User>?> SearchUsers(string query)
    {
        var users = await _database.SelectManyAsync<Models.User.User>(
            new Expr("Username", OperatorEnum.Contains, $"%{query}%").PrependOr("Id", OperatorEnum.Equals, query));

        return users;
    }

    public async Task<long> GetTotalUsers()
    {
        var exp = new Expr("Id", OperatorEnum.IsNotNull, null);
        return await _database.CountAsync<Models.User.User>(exp);
    }
}