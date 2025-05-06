using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Utils;

namespace Sunrise.Shared.Database.Services;

public class ScoreFileService(SunriseDbContext dbContext)
{
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

            dbContext.UserFiles.Add(record);
            await dbContext.SaveChangesAsync();

            return record;
        });
    }

    public async Task<byte[]?> GetReplayFile(int replayId, CancellationToken ct = default)
    {
        var record = await dbContext.UserFiles.AsNoTracking().FirstOrDefaultAsync(record => record.Id == replayId, ct);

        if (record == null)
            return null;

        var filePath = Path.Combine(DataPath, record.Path);
        var file = await LocalStorageRepository.ReadFileAsync(filePath, ct);

        return file;
    }
}