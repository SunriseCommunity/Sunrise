using ExpressionTree;
using Microsoft.Extensions.Logging;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Objects.Keys;
using Sunrise.Shared.Repositories;
using Watson.ORM.Sqlite;

namespace Sunrise.Shared.Database.Services.Beatmap.Services;

public class BeatmapFileService
{

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

    private static string DataPath => Configuration.DataPath;

    public async Task SetBeatmapFile(int beatmapId, byte[] beatmap)
    {
        var beatmapPath = $"Files/Beatmaps/{beatmapId}.osu";
        var filePath = Path.Combine(DataPath, beatmapPath);
        await File.WriteAllBytesAsync(filePath, beatmap);

        var record = new BeatmapFile
        {
            BeatmapId = beatmapId,
            Path = beatmapPath
        };

        record = await _database.InsertAsync(record);
        await _redis.Set(RedisKey.BeatmapRecord(beatmapId), record);
    }

    public async Task<byte[]?> GetBeatmapFile(int beatmapId)
    {
        var cachedRecord = await _redis.Get<BeatmapFile?>(RedisKey.BeatmapRecord(beatmapId));
        string? filePath;
        byte[]? file;

        if (cachedRecord != null)
        {
            filePath = Path.Combine(DataPath, cachedRecord.Path);
            file = await LocalStorageRepository.ReadFileAsync(filePath);
            return file;
        }

        var exp = new Expr("BeatmapId", OperatorEnum.Equals, beatmapId);
        var record = await _database.SelectFirstAsync<BeatmapFile?>(exp);

        if (record == null) return null;

        filePath = Path.Combine(DataPath, record.Path);
        file = await LocalStorageRepository.ReadFileAsync(filePath);

        if (file == null) return null;

        await _redis.Set(RedisKey.BeatmapRecord(beatmapId), record);

        return file;
    }
}