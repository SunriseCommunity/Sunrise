using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Utils;

namespace Sunrise.Shared.Database.Services.Users;

public class UserMetadataService(
    ILogger<UserMetadataService> logger,
    Lazy<DatabaseService> databaseService,
    SunriseDbContext dbContext)
{
    private readonly ILogger _logger = logger;

    private async Task<Result> AddUserMetadata(UserMetadata userMetadata)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            dbContext.UserMetadata.Add(userMetadata);
            await dbContext.SaveChangesAsync();
        });
    }

    public async Task<Result> UpdateUserMetadata(UserMetadata userMetadata)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            dbContext.UpdateEntity(userMetadata);
            await dbContext.SaveChangesAsync();
        });
    }

    public async Task<UserMetadata?> GetUserMetadata(int userId, CancellationToken ct = default)
    {
        var metadata = await dbContext.UserMetadata.Where(e => e.UserId == userId).FirstOrDefaultAsync(cancellationToken: ct);

        if (metadata == null)
        {
            var user = await databaseService.Value.Users.GetUser(userId, ct: ct);
            if (user == null) return null;

            _logger.LogInformation($"User metadata not found for user (id: {userId}). Creating new metadata.");

            metadata = new UserMetadata
            {
                UserId = user.Id,
                User = user
            };

            await AddUserMetadata(metadata);
        }

        return metadata;
    }
}