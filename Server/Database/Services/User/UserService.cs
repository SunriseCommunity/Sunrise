using ExpressionTree;
using Sunrise.Server.Application;
using Sunrise.Server.Database.Models.User;
using Sunrise.Server.Database.Services.User.Services;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types;
using Sunrise.Server.Types.Enums;
using Watson.ORM.Sqlite;
using GameMode = Sunrise.Server.Types.Enums.GameMode;

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
        if (await GetUser(username: user.Username) != null)
            throw new Exception("User with this username already exists");

        if (await GetUser(email: user.Email) != null)
            throw new Exception("User with this email already exists");

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
        await _redis.Remove(RedisKey.AllUsers());

        return user;
    }

    public async Task<Models.User.User?> GetUser(int? id = null, string? username = null, string? email = null,
        string? passhash = null, bool useCache = true)
    {
        var redisKeys = new List<string>
        {
            RedisKey.UserIdByEmail(email ?? "")
        };

        if (username != null)
        {
            if (passhash != null)
                redisKeys.Add(RedisKey.UserIdByUsernameAndPassHash(username, passhash));
            else
                redisKeys.Add(RedisKey.UserIdByUsername(username));
        }

        var cachedUserId = await _redis.Get<int?>([.. redisKeys]);
        if (cachedUserId != null && useCache) id = cachedUserId;

        var cachedUser = await _redis.Get<Models.User.User?>(RedisKey.UserById(id ?? 0));
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

        await _redis.Set(RedisKey.UserById(user.Id), user);
        await _redis.Set([RedisKey.UserIdByUsername(user.Username), RedisKey.UserIdByEmail(user.Email), RedisKey.UserIdByUsernameAndPassHash(user.Username, user.Passhash)], user.Id);

        return user;
    }


    public async Task UpdateUserUsername(Models.User.User user, string oldUsername, string newUsername, int? updatedById = null, string? userIp = null)
    {
        user.Username = newUsername;
        await UpdateUser(user);

        var ip = userIp ?? "";
        await _services.EventService.UserEvent.CreateNewUserChangeUsernameEvent(user.Id, ip, oldUsername, newUsername, updatedById);
    }

    public async Task UpdateUser(Models.User.User user)
    {
        var oldUser = await GetUser(id: user.Id);
        if (oldUser == null) throw new Exception("User not found");

        await _redis.Remove(
        [
            RedisKey.UserById(oldUser.Id),
            RedisKey.UserIdByUsername(oldUser.Username),
            RedisKey.UserIdByEmail(oldUser.Email),
            RedisKey.UserIdByUsernameAndPassHash(oldUser.Username, oldUser.Passhash)
        ]);

        await _database.UpdateAsync(user);

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        var session = sessions.GetSession(userId: user.Id);

        if (session != null)
            await session.UpdateUser(user);

        await _redis.Remove(RedisKey.AllUsers());

        await _redis.Set(RedisKey.UserById(user.Id), user);
        await _redis.Set([RedisKey.UserIdByUsername(user.Username), RedisKey.UserIdByEmail(user.Email), RedisKey.UserIdByUsernameAndPassHash(user.Username, user.Passhash)], user.Id);
    }

    public async Task<bool> DeleteUser(int userId)
    {
        var user = await GetUser(id: userId);
        if (user == null) return false;

        var isUserHasAnyLoginEvent = await _services.EventService.UserEvent.IsUserHasAnyLoginEvent(user.Id);
        var isUserHasAnyScore = await _services.ScoreService.GetUserLastScore(userId) != null;

        if (isUserHasAnyLoginEvent || isUserHasAnyScore || user.Username == Configuration.BotUsername)
        {
            _logger.LogWarning($"User {user.Username} has login events or some active score. Deleting user with any of these conditions is not allowed.");
            return false;
        }

        foreach (var mode in Enum.GetValues<GameMode>())
        {
            await Stats.DeleteUserStats(user.Id, mode);
            var userScores = await _services.ScoreService.GetUserScores(user.Id, mode, ScoreTableType.Recent);

            foreach (var score in userScores)
            {
                await _services.ScoreService.MarkAsDeleted(score);
            }
        }

        await Medals.DeleteUsersMedals(user.Id);
        await Stats.Snapshots.DeleteUserStatsSnapshot(user.Id);
        await Favourites.DeleteUsersFavouriteBeatmaps(user.Id);
        await Files.DeleteUsersFiles(user.Id);

        await _database.DeleteAsync(user);

        await _redis.Remove(
        [
            RedisKey.UserById(user.Id),
            RedisKey.UserIdByUsername(user.Username),
            RedisKey.UserIdByEmail(user.Email),
            RedisKey.UserIdByUsernameAndPassHash(user.Username, user.Passhash)
        ]);

        return true;
    }

    public async Task<List<Models.User.User>> GetAllUsers(bool useCache = true)
    {
        var cachedStats = await _redis.Get<List<Models.User.User>>(RedisKey.AllUsers());

        if (cachedStats != null && useCache) return cachedStats;

        var users = await _database.SelectManyAsync<Models.User.User>(new Expr("Id", OperatorEnum.IsNotNull, null));

        if (users == null) return [];

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
        var exp = new Expr("Id", OperatorEnum.IsNotNull, null).PrependAnd("AccountStatus", OperatorEnum.Equals, (int)UserAccountStatus.Active);
        return await _database.CountAsync<Models.User.User>(exp);
    }
}