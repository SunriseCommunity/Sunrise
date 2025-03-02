using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Utils;

namespace Sunrise.Shared.Database.Services;

public class BeatmapFileService
{
    private readonly DatabaseService _databaseService;
    private readonly SunriseDbContext _dbContext;

    public BeatmapFileService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
        _dbContext = databaseService.DbContext;
    }

    private static string DataPath => Configuration.DataPath;

    public async Task<Result> AddBeatmapFile(int beatmapId, byte[] beatmap)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var beatmapPath = $"Files/Beatmaps/{beatmapId}.osu";
            var filePath = Path.Combine(DataPath, beatmapPath);
            await File.WriteAllBytesAsync(filePath, beatmap);

            var record = new BeatmapFile
            {
                BeatmapId = beatmapId,
                Path = beatmapPath
            };

            _dbContext.BeatmapFiles.Add(record);
            await _dbContext.SaveChangesAsync();
        });
    }

    public async Task<byte[]?> GetBeatmapFile(int beatmapId)
    {
        var record = await _dbContext.BeatmapFiles.FirstOrDefaultAsync(r => r.BeatmapId == beatmapId);
        if (record == null) return null;

        var filePath = Path.Combine(DataPath, record.Path);
        var file = await LocalStorageRepository.ReadFileAsync(filePath);

        return file;
    }
}