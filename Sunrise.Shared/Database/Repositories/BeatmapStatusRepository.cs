using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Utils;

namespace Sunrise.Shared.Database.Repositories;

public class CustomBeatmapStatusRepository(ILogger<ScoreRepository> logger, SunriseDbContext dbContext)
{
    public async Task<CustomBeatmapStatus?> GetCustomBeatmapStatus(string beatmapHash, QueryOptions? options = null)
    {
        return await dbContext.CustomBeatmapStatuses
            .Where(m => m.BeatmapHash == beatmapHash)
            .UseQueryOptions(options)
            .FirstOrDefaultAsync();
    }

    public async Task<List<CustomBeatmapStatus>> GetCustomBeatmapSetStatuses(int beatmapSetId, QueryOptions? options = null)
    {
        return await dbContext.CustomBeatmapStatuses
            .Where(m => m.BeatmapSetId == beatmapSetId)
            .UseQueryOptions(options)
            .ToListAsync();
    }

    public async Task<Result> AddCustomBeatmapStatus(CustomBeatmapStatus status)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            dbContext.CustomBeatmapStatuses.Add(status);
            await dbContext.SaveChangesAsync();
        });
    }

    public async Task<Result> UpdateCustomBeatmapStatus(CustomBeatmapStatus status)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            dbContext.UpdateEntity(status);
            await dbContext.SaveChangesAsync();
        });
    }
    
    public async Task<Result> DeleteCustomBeatmapStatus(CustomBeatmapStatus status)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            dbContext.CustomBeatmapStatuses.Remove(status);
            await dbContext.SaveChangesAsync();
        });
    }
}