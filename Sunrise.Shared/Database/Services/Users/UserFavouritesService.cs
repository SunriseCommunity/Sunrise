using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Utils;

namespace Sunrise.Shared.Database.Services.Users;

public class UserFavouritesService
{
    private readonly DatabaseService _databaseService;
    private readonly SunriseDbContext _dbContext;

    public UserFavouritesService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
        _dbContext = databaseService.DbContext;
    }

    public async Task<Result> AddFavouriteBeatmap(int userId, int beatmapSetId)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var favourite = new UserFavouriteBeatmap
            {
                UserId = userId,
                BeatmapSetId = beatmapSetId
            };

            var favouriteExists = await _dbContext.UserFavouriteBeatmaps.AnyAsync(ufb => ufb.UserId == userId && ufb.BeatmapSetId == beatmapSetId);
            if (favouriteExists)
                throw new ApplicationException(QueryResultError.RECORD_WITH_SAME_KEY_ALREADY_EXIST);

            _dbContext.UserFavouriteBeatmaps.Add(favourite);
            await _dbContext.SaveChangesAsync();
        });
    }

    public async Task<Result> RemoveFavouriteBeatmap(int userId, int beatmapSetId)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var favourite = _dbContext.UserFavouriteBeatmaps.FirstOrDefault(ufb => ufb.UserId == userId && ufb.BeatmapSetId == beatmapSetId);
            if (favourite == null)
                throw new ApplicationException(QueryResultError.REQUESTED_RECORD_NOT_FOUND);

            _dbContext.UserFavouriteBeatmaps.Remove(favourite);
            await _dbContext.SaveChangesAsync();
        });
    }

    public async Task<bool> IsBeatmapSetFavourited(int userId, int beatmapSetId)
    {
        return await _dbContext.UserFavouriteBeatmaps.AsNoTracking().AnyAsync(ufb => ufb.UserId == userId && ufb.BeatmapSetId == beatmapSetId);
    }

    public async Task<List<int>> GetUserFavouriteBeatmapIds(int userId, QueryOptions? options = null)
    {
        return await _dbContext.UserFavouriteBeatmaps
            .Where(ufb => ufb.UserId == userId)
            .AsNoTracking()
            .UseQueryOptions(options)
            .Select(ufb => ufb.BeatmapSetId)
            .ToListAsync();
    }

    public async Task<int> GetUserFavouriteBeatmapIdsCount(int userId)
    {
        return await _dbContext.UserFavouriteBeatmaps
            .Where(ufb => ufb.UserId == userId)
            .AsNoTracking()
            .Select(ufb => ufb.BeatmapSetId)
            .CountAsync();
    }
}