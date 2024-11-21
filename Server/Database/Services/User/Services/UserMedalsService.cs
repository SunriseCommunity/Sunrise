using ExpressionTree;
using osu.Shared;
using Sunrise.Server.Database.Models.User;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types;
using Watson.ORM.Sqlite;

namespace Sunrise.Server.Database.Services.User.Services;

public class UserMedalsService
{
    private readonly WatsonORM _database;
    private readonly ILogger _logger;
    private readonly RedisRepository _redis;
    private readonly DatabaseManager _services;

    public UserMedalsService(DatabaseManager services, RedisRepository redis, WatsonORM database)
    {
        var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        _logger = loggerFactory.CreateLogger<UserMedalsService>();

        _services = services;
        _database = database;
        _redis = redis;
    }


    public async Task<List<UserMedals>> GetUserMedals(int userId, GameMode? mode = null)
    {
        var cachedMedals = await _redis.Get<List<UserMedals>>(RedisKey.UserMedals(userId, mode));
        if (cachedMedals != null) return cachedMedals;

        var exp = new Expr("UserId", OperatorEnum.Equals, userId);
        var userMedals = await _database.SelectManyAsync<UserMedals>(exp);

        if (userMedals == null) return [];

        if (mode != null)
        {
            var modeMedals = await _services.MedalService.GetMedals(mode.Value);
            userMedals = userMedals.Where(x => modeMedals.Any(y => y.Id == x.MedalId)).ToList();
        }

        await _redis.Set(RedisKey.UserMedals(userId, mode), userMedals);

        return userMedals;
    }

    public async Task UnlockMedal(int userId, int medalId)
    {
        var userMedal = new UserMedals
        {
            UserId = userId,
            MedalId = medalId
        };

        await _database.InsertAsync(userMedal);
        await _redis.Remove(RedisKey.UserMedals(userId));
    }
}