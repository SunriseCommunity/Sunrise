using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Utils;

namespace Sunrise.Shared.Database.Services.Users;

public class UserRelationshipService(
    ILogger<UserRelationshipService> logger,
    Lazy<DatabaseService> databaseService,
    SunriseDbContext dbContext)
{
    private readonly ILogger _logger = logger;

    private async Task<Result> AddUserRelationship(UserRelationship userRelationship)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            dbContext.UserRelationships.Add(userRelationship);
            await dbContext.SaveChangesAsync();
        });
    }

    public async Task<Result> UpdateUserRelationship(UserRelationship userRelationship)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            dbContext.UpdateEntity(userRelationship);
            await dbContext.SaveChangesAsync();
        });
    }

    public async Task<UserRelationship?> GetUserRelationship(int userId, int targetId, CancellationToken ct = default)
    {
        var relationship = await dbContext.UserRelationships
            .Where(r => 
                r.UserId == userId &&
                r.User.AccountStatus != UserAccountStatus.Restricted &&
                r.TargetId == targetId && 
                r.Target.AccountStatus != UserAccountStatus.Restricted)
            .FirstOrDefaultAsync(cancellationToken: ct);

        if (relationship == null)
        {
            var user = await databaseService.Value.Users.GetValidUser(userId, ct: ct);
            if (user == null) return null;
            
            var target = await databaseService.Value.Users.GetValidUser(targetId, ct: ct);
            if (target == null) return null;

            relationship = new UserRelationship
            {
                UserId = user.Id,
                TargetId = target.Id,
            };

            await AddUserRelationship(relationship);
        }

        return relationship;
    }

    public async Task<(List<UserRelationship> friends, int totalCount)> GetUserFriends(int userId, QueryOptions? options = null, CancellationToken ct = default)
    {
        var query = dbContext.UserRelationships
            .Where(r => r.UserId == userId && r.Relation == UserRelation.Friend && r.Target.AccountStatus != UserAccountStatus.Restricted);

        var totalCount = options?.IgnoreCountQueryIfExists == true ? -1 : await query.CountAsync(cancellationToken: ct);

        var friends = await query.UseQueryOptions(options).ToListAsync(cancellationToken: ct);

        return (friends, totalCount);
    }

    public async Task<(List<UserRelationship> friends, int totalCount)> GetUserFollowers(int userId, QueryOptions? options = null, CancellationToken ct = default)
    {
        var query = dbContext.UserRelationships
            .Where(r => r.TargetId == userId && r.Relation == UserRelation.Friend && r.User.AccountStatus != UserAccountStatus.Restricted);

        var totalCount = options?.IgnoreCountQueryIfExists == true ? -1 : await query.CountAsync(cancellationToken: ct);

        var friends = await query.UseQueryOptions(options).ToListAsync(cancellationToken: ct);

        return (friends, totalCount);
    }
}