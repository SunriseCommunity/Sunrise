using ExpressionTree;
using Microsoft.Extensions.Logging;
using Sunrise.Shared.Database.Models.User;
using Sunrise.Shared.Repositories;
using Watson.ORM.Sqlite;

namespace Sunrise.Shared.Database.Services.User.Services;

public class UserFavouritesService
{
    private readonly WatsonORM _database;
    private readonly ILogger _logger;
    private readonly RedisRepository _redis;

    public UserFavouritesService(DatabaseManager services, RedisRepository redis, WatsonORM database)
    {
        var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        _logger = loggerFactory.CreateLogger<UserFavouritesService>();

        _database = database;
        _redis = redis;
    }


    public async Task AddFavouriteBeatmap(int userId, int beatmapSetId)
    {
        var favourite = new UserFavouriteBeatmap
        {
            UserId = userId,
            BeatmapSetId = beatmapSetId
        };

        var exp = new Expr("UserId", OperatorEnum.Equals, userId).PrependAnd("BeatmapSetId",
            OperatorEnum.Equals,
            beatmapSetId);
        var favouriteExists = await _database.SelectFirstAsync<UserFavouriteBeatmap?>(exp);

        if (favouriteExists != null)
            return;

        await _database.InsertAsync(favourite);
    }

    public async Task RemoveFavouriteBeatmap(int userId, int beatmapSetId)
    {
        var exp = new Expr("UserId", OperatorEnum.Equals, userId).PrependAnd("BeatmapSetId",
            OperatorEnum.Equals,
            beatmapSetId);
        var favourite = await _database.SelectFirstAsync<UserFavouriteBeatmap?>(exp);

        if (favourite == null)
            return;

        await _database.DeleteAsync(favourite);
    }

    public async Task<bool> IsBeatmapSetFavourited(int userId, int beatmapSetId)
    {
        var exp = new Expr("UserId", OperatorEnum.Equals, userId).PrependAnd("BeatmapSetId",
            OperatorEnum.Equals,
            beatmapSetId);
        var favourite = await _database.SelectFirstAsync<UserFavouriteBeatmap?>(exp);

        return favourite != null;
    }

    public async Task<List<int>> GetUserFavouriteBeatmaps(int userId)
    {
        var exp = new Expr("UserId", OperatorEnum.Equals, userId);
        var favourites = await _database.SelectManyAsync<UserFavouriteBeatmap>(exp);

        return favourites.Select(x => x.BeatmapSetId).ToList();
    }

    public async Task DeleteUsersFavouriteBeatmaps(int userId)
    {
        var exp = new Expr("UserId", OperatorEnum.Equals, userId);
        await _database.DeleteManyAsync<UserFavouriteBeatmap>(exp);
    }
}