using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Utils;

namespace Sunrise.Shared.Database.Services.Users;

public class UserFavouritesService(SunriseDbContext dbContext)
{
    public async Task<Result> AddFavouriteBeatmap(int userId, int beatmapSetId)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var favourite = new UserFavouriteBeatmap
            {
                UserId = userId,
                BeatmapSetId = beatmapSetId
            };

            var favouriteExists = await dbContext.UserFavouriteBeatmaps.AnyAsync(ufb => ufb.UserId == userId && ufb.BeatmapSetId == beatmapSetId);
            if (favouriteExists)
                throw new ApplicationException(QueryResultError.RECORD_WITH_SAME_KEY_ALREADY_EXIST);

            dbContext.UserFavouriteBeatmaps.Add(favourite);
            await dbContext.SaveChangesAsync();
        });
    }

    public async Task<Result> RemoveFavouriteBeatmap(int userId, int beatmapSetId)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var favourite = dbContext.UserFavouriteBeatmaps.FirstOrDefault(ufb => ufb.UserId == userId && ufb.BeatmapSetId == beatmapSetId);
            if (favourite == null)
                throw new ApplicationException(QueryResultError.REQUESTED_RECORD_NOT_FOUND);

            dbContext.UserFavouriteBeatmaps.Remove(favourite);
            await dbContext.SaveChangesAsync();
        });
    }

    public async Task<bool> IsBeatmapSetFavourited(int userId, int beatmapSetId, CancellationToken ct = default)
    {
        return await dbContext.UserFavouriteBeatmaps.AsNoTracking().AnyAsync(ufb => ufb.UserId == userId && ufb.BeatmapSetId == beatmapSetId, ct);
    }

    public async Task<(List<int>, int)> GetUserFavouriteBeatmapIds(int userId, QueryOptions? options = null, CancellationToken ct = default)
    {
        var beatmapsQuery = dbContext.UserFavouriteBeatmaps
            .Where(ufb => ufb.UserId == userId)
            .AsNoTracking();

        var totalCount = options?.IgnoreCountQueryIfExists == true ? -1 : await beatmapsQuery.CountAsync(cancellationToken: ct);

        var beatmapsIds = await beatmapsQuery
            .UseQueryOptions(options)
            .Select(ufb => ufb.BeatmapSetId)
            .ToListAsync(cancellationToken: ct);

        return (beatmapsIds, totalCount);
    }
}