using ExpressionTree;
using Microsoft.Extensions.Logging;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Objects.Keys;
using Sunrise.Shared.Repositories;
using Watson.ORM.Sqlite;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Database.Services;

public class MedalService
{
    private readonly WatsonORM _database;
    private readonly ILogger _logger;
    private readonly RedisRepository _redis;

    public MedalService(DatabaseManager services, RedisRepository redis, WatsonORM database)
    {
        var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        _logger = loggerFactory.CreateLogger<MedalService>();

        _database = database;
        _redis = redis;
    }

    public async Task<List<Medal>> GetMedals(GameMode mode)
    {
        var cachedMedals = await _redis.Get<List<Medal>>(RedisKey.AllMedals(mode));
        if (cachedMedals != null) return cachedMedals;

        var exp = new Expr("GameMode", OperatorEnum.Equals, (int)mode).PrependOr("GameMode", OperatorEnum.IsNull, null);

        var medals = await _database.SelectManyAsync<Medal>(exp);
        if (medals == null) return [];

        await _redis.Set(RedisKey.AllMedals(mode), medals);

        return medals;
    }

    public async Task<Medal?> GetMedal(int medalId)
    {
        var cachedMedal = await _redis.Get<Medal?>(RedisKey.Medal(medalId));
        if (cachedMedal != null) return cachedMedal;

        var exp = new Expr("Id", OperatorEnum.Equals, medalId);

        var medal = await _database.SelectFirstAsync<Medal?>(exp);
        if (medal == null) return null;

        await _redis.Set(RedisKey.Medal(medalId), medal);

        return medal;
    }

    public async Task<byte[]?> GetMedalImage(int medalFileId, bool isHighRes = false)
    {
        var cachedRecord = await _redis.Get<MedalFile>(RedisKey.MedalImageRecord(medalFileId));
        byte[]? file;

        if (cachedRecord != null)
        {
            var cachedImagePath = isHighRes ? cachedRecord.Path.Replace(".png", "@2x.png") : cachedRecord.Path;
            file = await LocalStorageRepository.ReadFileAsync(Path.Combine(Configuration.DataPath, cachedImagePath));
            return file;
        }

        var exp = new Expr("Id", OperatorEnum.Equals, medalFileId);
        var record = await _database.SelectFirstAsync<MedalFile?>(exp);

        if (record == null)
            return null;

        var imagePath = isHighRes ? record.Path.Replace(".png", "@2x.png") : record.Path;
        file = await LocalStorageRepository.ReadFileAsync(Path.Combine(Configuration.DataPath, imagePath));
        if (file == null)
            return null;

        await _redis.Set(RedisKey.MedalImageRecord(medalFileId), record);

        return file;
    }
}