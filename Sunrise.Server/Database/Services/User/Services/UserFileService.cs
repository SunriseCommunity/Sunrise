using ExpressionTree;
using Sunrise.Server.Application;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Repositories;
using Sunrise.Server.Storages;
using Sunrise.Server.Types;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;
using Watson.ORM.Sqlite;

namespace Sunrise.Server.Database.Services.User.Services;

public class UserFileService
{
    private static string DataPath => Configuration.DataPath;
    private readonly WatsonORM _database;

    private readonly ILogger _logger;
    private readonly RedisRepository _redis;

    public UserFileService(DatabaseManager services, RedisRepository redis, WatsonORM database)
    {
        var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        _logger = loggerFactory.CreateLogger<UserFileService>();

        _database = database;
        _redis = redis;
    }

    public async Task<bool> SetAvatar(int userId, byte[] avatar)
    {
        var imagePath =  $"Files/Avatars/{userId}.png";
        var filePath = Path.Combine(DataPath, imagePath);

        if (!await LocalStorage.WriteFileAsync(filePath, ImageTools.ResizeImage(avatar, 256, 256)))
            return false;

        var record = new UserFile
        {
            OwnerId = userId,
            Path = imagePath,
            Type = FileType.Avatar
        };

        var exp = new Expr("OwnerId", OperatorEnum.Equals, userId).PrependAnd("Type", OperatorEnum.Equals, (int)FileType.Avatar);
        var prevRecord = await _database.SelectFirstAsync<UserFile?>(exp);

        if (prevRecord != null)
        {
            record.Id = prevRecord.Id;
            record.CreatedAt = prevRecord.CreatedAt;
            record = await _database.UpdateAsync(record);

        }
        else
        {
            record = await _database.InsertAsync(record);
        }

        if (record == null)
            return false;

        await _redis.Set(RedisKey.AvatarRecord(userId), record);
        return true;
    }

    public async Task<byte[]?> GetAvatar(int userId, bool fallToDefault = true)
    {
        var cachedRecord = await _redis.Get<UserFile>(RedisKey.AvatarRecord(userId));
        string? filePath;
        byte[]? file;

        if (cachedRecord != null)
        {
            filePath = Path.Combine(DataPath, cachedRecord.Path);
            file = await LocalStorage.ReadFileAsync(filePath);
            return file;
        }

        var exp = new Expr("OwnerId", OperatorEnum.Equals, userId);
        exp.PrependAnd("Type", OperatorEnum.Equals, (int)FileType.Avatar);

        var record = await _database.SelectFirstAsync<UserFile?>(exp);

        if (record == null && !fallToDefault) return null;

        filePath = Path.Combine(DataPath, record?.Path ?? $"Files/Avatars/Default.png");
        file = await LocalStorage.ReadFileAsync(filePath);
        if (file == null)
            return null;

        await _redis.Set(RedisKey.AvatarRecord(userId), record);

        return file;
    }

    // TODO: Rename to insert?
    public async Task<int> SetScreenshot(int userId, byte[] screenshot)
    {
        var imagePath = $"Files/Screenshot/{userId}-{DateTime.UtcNow:yyyy-MM-dd_HH-mm-ss}.jpg";
        var filePath = Path.Combine(DataPath, imagePath);
        await File.WriteAllBytesAsync(filePath, screenshot);

        var record = new UserFile
        {
            OwnerId = userId,
            Path = imagePath,
            Type = FileType.Screenshot
        };

        record = await _database.InsertAsync(record);
        await _redis.Set(RedisKey.ScreenshotRecord(userId), record);

        return record.Id;
    }

    public async Task<byte[]?> GetScreenshot(int screenshotId)
    {
        var cachedRecord = await _redis.Get<UserFile>(RedisKey.ScreenshotRecord(screenshotId));
        string? filePath;
        byte[]? file;

        if (cachedRecord != null)
        {
            filePath = Path.Combine(DataPath, cachedRecord.Path);
            file = await LocalStorage.ReadFileAsync(filePath);
            return file;
        }

        var exp = new Expr("Id", OperatorEnum.Equals, screenshotId);
        var record = await _database.SelectFirstAsync<UserFile?>(exp);

        if (record == null)
            return null;

        filePath = Path.Combine(DataPath, record.Path);
        file = await LocalStorage.ReadFileAsync(filePath);
        if (file == null)
            return null;

        await _redis.Set(RedisKey.ScreenshotRecord(screenshotId), record);

        return file;
    }

    public async Task<bool> SetBanner(int userId, byte[] banner)
    {
        var imagePath =  $"Files/Banners/{userId}.png";
        var filePath = Path.Combine(DataPath, imagePath);
        
        if (!await LocalStorage.WriteFileAsync(filePath, ImageTools.ResizeImage(banner, 1280, 320)))
            return false;

        var record = new UserFile
        {
            OwnerId = userId,
            Path = imagePath,
            Type = FileType.Banner
        };

        var exp = new Expr("OwnerId", OperatorEnum.Equals, userId).PrependAnd("Type", OperatorEnum.Equals, (int)FileType.Banner);
        var prevRecord = await _database.SelectFirstAsync<UserFile?>(exp);

        if (prevRecord != null)
        {
            record.Id = prevRecord.Id;
            record.CreatedAt = prevRecord.CreatedAt;
            record = await _database.UpdateAsync(record);
        }
        else
        {
            record = await _database.InsertAsync(record);
        }

        if (record == null)
            return false;

        await _redis.Set(RedisKey.BannerRecord(userId), record);
        return true;
    }

    public async Task<byte[]?> GetBanner(int userId, bool fallToDefault = true)
    {
        var cachedRecord = await _redis.Get<UserFile>(RedisKey.BannerRecord(userId));
        string? filePath;
        byte[]? file;

        if (cachedRecord != null)
        {
            filePath = Path.Combine(DataPath, cachedRecord.Path);
            file = await LocalStorage.ReadFileAsync(filePath);
            return file;
        }

        var exp = new Expr("OwnerId", OperatorEnum.Equals, userId).PrependAnd("Type", OperatorEnum.Equals, (int)FileType.Banner);
        var record = await _database.SelectFirstAsync<UserFile?>(exp);

        if (record == null && !fallToDefault) return null;

        filePath = Path.Combine(DataPath, record?.Path ?? "Files/Banners/Default.png");
        file = await LocalStorage.ReadFileAsync(filePath);
        if (file == null)
            return null;

        await _redis.Set(RedisKey.BannerRecord(userId), record);

        return file;
    }

    public async Task DeleteUsersFiles(int userId)
    {
        var exp = new Expr("OwnerId", OperatorEnum.Equals, userId);
        await _database.DeleteManyAsync<UserFile>(exp);
    }
}