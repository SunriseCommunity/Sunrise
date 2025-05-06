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

public class UserFileService(SunriseDbContext dbContext)
{
    private static string DataPath => Configuration.DataPath;

    public async Task<Result> AddOrUpdateAvatar(int userId, Stream fileStream)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var imageType = ImageTools.GetImageType(fileStream);
            var imagePath = $"Files/Avatars/{userId}.{imageType}";
            var filePath = Path.Combine(DataPath, imagePath);

            if (!await LocalStorageRepository.WriteFileAsync(filePath, ImageTools.ResizeImage(fileStream, 256, 256)))
                throw new ApplicationException(QueryResultError.CREATING_FILE_FAILED);

            var record = new UserFile
            {
                OwnerId = userId,
                Path = imagePath,
                Type = FileType.Avatar
            };

            var prevRecord = await dbContext.UserFiles.FirstOrDefaultAsync(x => x.OwnerId == userId && x.Type == FileType.Avatar);

            if (prevRecord == null)
            {
                dbContext.UserFiles.Add(record);
                await dbContext.SaveChangesAsync();
                return;
            }

            var prevRecordFilePath = Path.Combine(DataPath, prevRecord.Path);
            if (prevRecordFilePath != filePath && File.Exists(prevRecordFilePath))
                File.Delete(prevRecordFilePath);

            prevRecord.UpdatedAt = DateTime.Now;
            prevRecord.Path = imagePath;

            await dbContext.SaveChangesAsync();
        });
    }

    public async Task<byte[]?> GetAvatar(int userId, bool fallToDefault = true, CancellationToken ct = default)
    {
        var record = await dbContext.UserFiles.AsNoTracking().FirstOrDefaultAsync(x => x.OwnerId == userId && x.Type == FileType.Avatar, ct);

        if (record == null && !fallToDefault) return null;

        var filePath = Path.Combine(DataPath, record?.Path ?? "Files/Avatars/Default.png");
        var file = await LocalStorageRepository.ReadFileAsync(filePath, ct);

        return file;
    }

    public async Task<Result<int>> AddScreenshot(int userId, Stream fileStream)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var imageType = ImageTools.GetImageType(fileStream);
            var imagePath = $"Files/Screenshot/{userId}-{DateTime.UtcNow:yyyy-MM-dd_HH-mm-ss}.{imageType}";
            var filePath = Path.Combine(DataPath, imagePath);

            if (!await LocalStorageRepository.WriteFileAsync(filePath, fileStream))
                throw new ApplicationException(QueryResultError.CREATING_FILE_FAILED);

            var record = new UserFile
            {
                OwnerId = userId,
                Path = imagePath,
                Type = FileType.Screenshot
            };

            dbContext.UserFiles.Add(record);
            await dbContext.SaveChangesAsync();

            return record.Id;
        });
    }

    public async Task<byte[]?> GetScreenshot(int screenshotId, CancellationToken ct = default)
    {
        var record = await dbContext.UserFiles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == screenshotId && x.Type == FileType.Screenshot, ct);

        if (record == null)
            return null;

        var filePath = Path.Combine(DataPath, record.Path);
        var file = await LocalStorageRepository.ReadFileAsync(filePath, ct);

        return file;
    }

    public async Task<Result> AddOrUpdateBanner(int userId, Stream fileStream)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var imageType = ImageTools.GetImageType(fileStream);
            var imagePath = $"Files/Banners/{userId}.{imageType}";
            var filePath = Path.Combine(DataPath, imagePath);

            if (!await LocalStorageRepository.WriteFileAsync(filePath, ImageTools.ResizeImage(fileStream, 1280, 320)))
                throw new ApplicationException(QueryResultError.CREATING_FILE_FAILED);

            var record = new UserFile
            {
                OwnerId = userId,
                Path = imagePath,
                Type = FileType.Banner
            };

            var prevRecord = await dbContext.UserFiles.FirstOrDefaultAsync(x => x.OwnerId == userId && x.Type == FileType.Banner);

            if (prevRecord == null)
            {
                dbContext.UserFiles.Add(record);
                await dbContext.SaveChangesAsync();
                return;
            }

            var prevRecordFilePath = Path.Combine(DataPath, prevRecord.Path);
            if (prevRecordFilePath != filePath && File.Exists(prevRecordFilePath))
                File.Delete(prevRecordFilePath);

            prevRecord.UpdatedAt = DateTime.Now;
            prevRecord.Path = imagePath;

            await dbContext.SaveChangesAsync();
        });
    }

    public async Task<byte[]?> GetBanner(int userId, bool fallToDefault = true, CancellationToken ct = default)
    {
        var record = await dbContext.UserFiles.AsNoTracking().FirstOrDefaultAsync(x => x.OwnerId == userId && x.Type == FileType.Banner, ct);

        if (record == null && !fallToDefault) return null;

        var filePath = Path.Combine(DataPath, record?.Path ?? "Files/Banners/Default.png");
        var file = await LocalStorageRepository.ReadFileAsync(filePath, ct);
        return file;
    }
}