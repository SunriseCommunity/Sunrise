using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Utils;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Database.Services.Users;

public class UserMedalsService(Lazy<DatabaseService> databaseService, SunriseDbContext dbContext)
{
    public async Task<List<UserMedals>> GetUserMedals(int userId, GameMode? mode = null, QueryOptions? options = null, CancellationToken ct = default)
    {
        var userMedalsQuery = dbContext.UserMedals.Where(um => um.UserId == userId);

        if (mode != null)
        {
            var modeMedals = await databaseService.Value.Medals.GetMedals(mode.Value, ct: ct);
            userMedalsQuery = userMedalsQuery.Where(x => modeMedals.Select(m => m.Id).Any(id => id == x.MedalId));
        }

        var userMedals = await userMedalsQuery
            .UseQueryOptions(options)
            .ToListAsync(cancellationToken: ct);

        return userMedals;
    }

    public async Task<Result> UnlockMedal(int userId, int medalId)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var userMedal = new UserMedals
            {
                UserId = userId,
                MedalId = medalId
            };

            dbContext.UserMedals.Add(userMedal);
            await dbContext.SaveChangesAsync();
        });
    }
}