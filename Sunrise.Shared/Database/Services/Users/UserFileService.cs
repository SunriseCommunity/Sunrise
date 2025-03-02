using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Utils;
using Sunrise.Shared.Utils.Tools;

namespace Sunrise.Shared.Database.Services.Users;

public class UserFileService
{
    private readonly DatabaseService _databaseService;
    private readonly SunriseDbContext _dbContext;

    public UserFileService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
        _dbContext = databaseService.DbContext;
    }

    private static string DataPath => Configuration.DataPath;

    public async Task<Result> AddOrUpdateAvatar(int userId, byte[] avatar)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var imagePath = $"Files/Avatars/{userId}.png";
            var filePath = Path.Combine(DataPath, imagePath);
            
            if (!await LocalStorageRepository.WriteFileAsync(filePath, ImageTools.ResizeImage(avatar, 256, 256)))
                throw new ApplicationException(QueryResultError.CREATING_FILE_FAILED);

            var record = new UserFile
            {
                OwnerId = userId,
                Path = imagePath,
                Type = FileType.Avatar
            };

            var prevRecord = await _dbContext.UserFiles.FirstOrDefaultAsync(x => x.OwnerId == userId && x.Type == FileType.Avatar);

            if (prevRecord == null)
            {
               _dbContext.UserFiles.Add(record);
               await _dbContext.SaveChangesAsync();
               return;
            }
            
            prevRecord.UpdatedAt = DateTime.Now;
            prevRecord.Path = imagePath;

            await _dbContext.SaveChangesAsync();
        });
    }

    public async Task<byte[]?> GetAvatar(int userId, bool fallToDefault = true)
    {
        var record = await _dbContext.UserFiles.FirstOrDefaultAsync(x => x.OwnerId == userId && x.Type == FileType.Avatar);

        if (record == null && !fallToDefault) return null;

        var filePath = Path.Combine(DataPath, record?.Path ?? "Files/Avatars/Default.png");
        var file = await LocalStorageRepository.ReadFileAsync(filePath);

        return file;
    }
    
    public async Task<Result<int>> AddScreenshot(int userId, byte[] screenshot)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var imagePath = $"Files/Screenshot/{userId}-{DateTime.UtcNow:yyyy-MM-dd_HH-mm-ss}.jpg";
            var filePath = Path.Combine(DataPath, imagePath);
            
            if (!await LocalStorageRepository.WriteFileAsync(filePath, screenshot))
                throw new ApplicationException(QueryResultError.CREATING_FILE_FAILED);
            
            var record = new UserFile
            {
                OwnerId = userId,
                Path = imagePath,
                Type = FileType.Screenshot
            };

            _dbContext.UserFiles.Add(record);
            await _dbContext.SaveChangesAsync();

            return record.Id;
        });
    }

    public async Task<byte[]?> GetScreenshot(int screenshotId)
    {
        var record = await _dbContext.UserFiles.FirstOrDefaultAsync(x => x.Id == screenshotId && x.Type == FileType.Screenshot);

        if (record == null)
            return null;

        var filePath = Path.Combine(DataPath, record.Path);
        var file = await LocalStorageRepository.ReadFileAsync(filePath);

        return file;
    }

    public async Task<Result> AddOrUpdateBanner(int userId, byte[] banner)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var imagePath = $"Files/Banners/{userId}.png";
            var filePath = Path.Combine(DataPath, imagePath);

            if (!await LocalStorageRepository.WriteFileAsync(filePath, ImageTools.ResizeImage(banner, 1280, 320)))
                throw new ApplicationException(QueryResultError.CREATING_FILE_FAILED);

            var record = new UserFile
            {
                OwnerId = userId,
                Path = imagePath,
                Type = FileType.Banner
            };

            var prevRecord = await _dbContext.UserFiles.FirstOrDefaultAsync(x => x.OwnerId == userId && x.Type == FileType.Banner);

            if (prevRecord == null)
            {
                _dbContext.UserFiles.Add(record);
                await _dbContext.SaveChangesAsync();
                return;
            }
            
            prevRecord.UpdatedAt = DateTime.Now;
            prevRecord.Path = imagePath;

            await _dbContext.SaveChangesAsync();
        });
    }

    public async Task<byte[]?> GetBanner(int userId, bool fallToDefault = true)
    {
        var record = await _dbContext.UserFiles.FirstOrDefaultAsync(x => x.OwnerId == userId && x.Type == FileType.Banner);

        if (record == null && !fallToDefault) return null;

        var filePath = Path.Combine(DataPath, record?.Path ?? "Files/Banners/Default.png");
        var file = await LocalStorageRepository.ReadFileAsync(filePath);
        return file;
    }
}