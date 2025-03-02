using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Utils;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Database.Services.Users;

public class UserMedalsService
{
    private readonly SunriseDbContext _dbContext;
    private readonly DatabaseService _databaseService;
    
    public UserMedalsService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
        _dbContext = databaseService.DbContext;
    }
    
    public async Task<List<UserMedals>> GetUserMedals(int userId, GameMode? mode = null, QueryOptions? options = null)
    {
        var userMedalsQuery = _dbContext.UserMedals.Where(um => um.UserId == userId);

        if (mode != null)
        {
            var modeMedals = await _databaseService.Medals.GetMedals(mode.Value);
            userMedalsQuery = userMedalsQuery.Where(x => modeMedals.Select(m => m.Id).Any(id => id == x.MedalId));
        }

        var userMedals = await userMedalsQuery
            .UseQueryOptions(options)
            .ToListAsync();

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

            _dbContext.UserMedals.Add(userMedal);
            await _dbContext.SaveChangesAsync();
        });
    }
}