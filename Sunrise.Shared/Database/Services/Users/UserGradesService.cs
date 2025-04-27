using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Utils;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Database.Services.Users;

public class UserGradesService(
    ILogger<UserGradesService> logger,
    Lazy<DatabaseService> databaseService,
    SunriseDbContext dbContext)
{
    private readonly ILogger _logger = logger;

    private async Task<Result> AddUserGrades(UserGrades userGrades)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            dbContext.UserGrades.Add(userGrades);
            await dbContext.SaveChangesAsync();
        });
    }

    public async Task<Result> UpdateUserGrades(UserGrades userGrades)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            dbContext.UpdateEntity(userGrades);
            await dbContext.SaveChangesAsync();
        });
    }

    public async Task<UserGrades?> GetUserGrades(int userId, GameMode mode, CancellationToken ct = default)
    {
        var grades = await dbContext.UserGrades.Where(e => e.UserId == userId && e.GameMode == mode).FirstOrDefaultAsync(cancellationToken: ct);

        if (grades == null)
        {
            var user = await databaseService.Value.Users.GetUser(userId, ct: ct);
            if (user == null) return null;

            _logger.LogInformation($"User grades not found for user (id: {userId}) in mode {mode}. Creating new grades.");

            grades = new UserGrades
            {
                UserId = user.Id,
                GameMode = mode
            };

            await AddUserGrades(grades);
        }

        return grades;
    }
}