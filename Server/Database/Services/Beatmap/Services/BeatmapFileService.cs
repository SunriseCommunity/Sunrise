using ExpressionTree;
using Sunrise.Server.Application;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Repositories;
using Sunrise.Server.Storages;
using Sunrise.Server.Types;
using Watson.ORM.Sqlite;

namespace Sunrise.Server.Database.Services.Beatmap.Services;

public class BeatmapFileService
{
    private const string DataPath = Configuration.DataPath;

    private readonly WatsonORM _database;

    private readonly ILogger _logger;
    private readonly RedisRepository _redis;

    public BeatmapFileService(DatabaseManager services, RedisRepository redis, WatsonORM database)
    {
        var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        _logger = loggerFactory.CreateLogger<BeatmapFileService>();

        _database = database;
        _redis = redis;
    }

    public async Task SetBeatmapFile(int beatmapId, byte[] beatmap)
    {
        var filePath = $"{DataPath}Files/Beatmaps/{beatmapId}.osu";
        await File.WriteAllBytesAsync(filePath, beatmap);

        var record = new BeatmapFile
        {
            BeatmapId = beatmapId,
            Path = filePath
        };

        record = await _database.InsertAsync(record);
        await _redis.Set(RedisKey.BeatmapRecord(beatmapId), record);
    }

    public async Task<byte[]?> GetBeatmapFile(int beatmapId)
    {
        var cachedRecord = await _redis.Get<BeatmapFile?>(RedisKey.BeatmapRecord(beatmapId));
        byte[]? file;

        if (cachedRecord != null)
        {
            file = await LocalStorage.ReadFileAsync(cachedRecord.Path);
            return file;
        }

        var exp = new Expr("BeatmapId", OperatorEnum.Equals, beatmapId);
        var record = await _database.SelectFirstAsync<BeatmapFile?>(exp);

        if (record == null) return null;

        file = await LocalStorage.ReadFileAsync(record.Path);

        if (file == null) return null;

        await _redis.Set(RedisKey.BeatmapRecord(beatmapId), record);

        return file;
    }
}