using ExpressionTree;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Objects.Keys;
using Sunrise.Shared.Repositories;
using Watson.ORM.Sqlite;

namespace Sunrise.Shared.Database.Services.Score.Services;

public class ScoreFileService
{

    private readonly WatsonORM _database;

    private readonly ILogger _logger;
    private readonly RedisRepository _redis;

    public ScoreFileService(DatabaseManager services, RedisRepository redis, WatsonORM database)
    {
        var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        _logger = loggerFactory.CreateLogger<ScoreFileService>();

        _database = database;
        _redis = redis;
    }

    private static string DataPath => Configuration.DataPath;


    // TODO: Rename to insert?
    public async Task<UserFile> UploadReplay(int userId, IFormFile replay)
    {
        var replayName = $"{userId}-{DateTime.UtcNow:yyyy-MM-dd_HH-mm-ss}.osr";
        var replayFile = $"Files/Replays/{replayName}";

        var filePath = Path.Combine(DataPath, replayFile);

        await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite);
        await replay.CopyToAsync(stream);
        stream.Close();

        var record = new UserFile
        {
            OwnerId = userId,
            Path = replayFile,
            Type = FileType.Replay
        };

        record = await _database.InsertAsync(record);
        await _redis.Set(RedisKey.ReplayRecord(record.Id), record);
        return record;
    }

    public async Task<byte[]?> GetReplay(int replayId)
    {
        var cachedRecord = await _redis.Get<UserFile>(RedisKey.ReplayRecord(replayId));
        string? filePath;
        byte[]? file;

        if (cachedRecord != null)
        {
            filePath = Path.Combine(DataPath, cachedRecord.Path);
            file = await LocalStorageRepository.ReadFileAsync(filePath);
            return file;
        }

        var exp = new Expr("Id", OperatorEnum.Equals, replayId);
        var record = await _database.SelectFirstAsync<UserFile?>(exp);

        if (record == null)
            return null;

        filePath = Path.Combine(DataPath, record.Path);
        file = await LocalStorageRepository.ReadFileAsync(filePath);
        if (file == null)
            return null;

        await _redis.Set(RedisKey.ReplayRecord(replayId), record);

        return file;
    }
}