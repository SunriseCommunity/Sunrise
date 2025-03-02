using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Http;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Utils;

namespace Sunrise.Shared.Database.Services;

public class ScoreFileService
{
    private readonly DatabaseService _databaseService;
    private readonly SunriseDbContext _dbContext;

    public ScoreFileService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
        _dbContext = databaseService.DbContext;
    }

    private static string DataPath => Configuration.DataPath;

    public async Task<Result<UserFile>> AddReplayFile(int userId, IFormFile replay)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
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

            _dbContext.UserFiles.Add(record);
            await _dbContext.SaveChangesAsync();

            return record;
        });
    }

    public async Task<byte[]?> GetReplayFile(int replayId)
    {
        var record = _dbContext.UserFiles.FirstOrDefault(record => record.Id == replayId);

        if (record == null)
            return null;

        var filePath = Path.Combine(DataPath, record.Path);
        var file = await LocalStorageRepository.ReadFileAsync(filePath);

        return file;
    }
}