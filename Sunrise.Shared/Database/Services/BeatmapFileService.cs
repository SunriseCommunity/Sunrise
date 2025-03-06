using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Utils;

namespace Sunrise.Shared.Database.Services;

public class BeatmapFileService(SunriseDbContext dbContext)
{
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

            dbContext.BeatmapFiles.Add(record);
            await dbContext.SaveChangesAsync();
        });
    }

    public async Task<byte[]?> GetBeatmapFile(int beatmapId)
    {
        var record = await dbContext.BeatmapFiles.FirstOrDefaultAsync(r => r.BeatmapId == beatmapId);
        if (record == null) return null;

        var filePath = Path.Combine(DataPath, record.Path);
        var file = await LocalStorageRepository.ReadFileAsync(filePath);

        return file;
    }
}