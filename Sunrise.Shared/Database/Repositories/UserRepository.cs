using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Database.Services.Users;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Utils;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Database.Repositories;

public class UserRepository(
    ILogger<UserRepository> logger,
    Lazy<DatabaseService> databaseService,
    SunriseDbContext dbContext,
    UserStatsService userStatsService,
    UserModerationService userModerationService,
    UserMedalsService userMedalsService,
    UserFavouritesService userFavouritesService,
    UserFileService userFileService,
    UserGradesService userGradesService)
{
    private readonly ILogger _logger = logger;

    public UserStatsService Stats { get; } = userStatsService;
    public UserFavouritesService Favourites { get; } = userFavouritesService;
    public UserMedalsService Medals { get; } = userMedalsService;
    public UserModerationService Moderation { get; } = userModerationService;
    public UserFileService Files { get; } = userFileService;
    public UserGradesService Grades { get; } = userGradesService;

    public async Task<Result> AddUser(User user)
    {
        return await databaseService.Value.CommitAsTransactionAsync(async () =>
        {
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();

            var modes = Enum.GetValues<GameMode>();

            foreach (var mode in modes)
            {
                var stats = new UserStats
                {
                    UserId = user.Id,
                    GameMode = mode
                };

                await Stats.AddUserStats(stats, user);
            }

            await dbContext.SaveChangesAsync();
        });
    }

    public async Task<User?> GetUser(
        long? id = null,
        string? username = null,
        string? email = null,
        string? passhash = null,
        QueryOptions? options = null,
        CancellationToken ct = default
    )
    {
        if (passhash != null && id == null && username == null && email == null)
            throw new ArgumentException("Passhash provided without any other parameters");

        var userQuery = dbContext.Users.AsQueryable();

        if (id != null) userQuery = userQuery.Where(u => u.Id == id);
        if (username != null) userQuery = userQuery.Where(u => u.Username == username);
        if (email != null) userQuery = userQuery.Where(u => u.Email == email);
        if (passhash != null) userQuery = userQuery.Where(u => u.Passhash == passhash);

        var user = await userQuery
            .IncludeUserThumbnails()
            .UseQueryOptions(options)
            .FirstOrDefaultAsync(cancellationToken: ct);

        return user;
    }


    public async Task<User?> GetValidUser(
        int? id = null,
        string? username = null,
        string? email = null,
        string? passhash = null,
        QueryOptions? options = null,
        CancellationToken ct = default
    )
    {
        if (passhash != null && id == null && username == null && email == null)
            throw new ArgumentException("Passhash provided without any other parameters");

        var userQuery = dbContext.Users.AsQueryable();

        if (id != null) userQuery = userQuery.Where(u => u.Id == id);
        if (username != null) userQuery = userQuery.Where(u => u.Username == username);
        if (email != null) userQuery = userQuery.Where(u => u.Email == email);
        if (passhash != null) userQuery = userQuery.Where(u => u.Passhash == passhash);

        var user = await userQuery
            .FilterValidUsers()
            .IncludeUserThumbnails()
            .UseQueryOptions(options)
            .FirstOrDefaultAsync(cancellationToken: ct);

        return user;
    }

    public async Task<List<User>> GetUsers(List<int>? ids = null, QueryOptions? options = null, CancellationToken ct = default)
    {
        var userQuery = dbContext.Users.AsQueryable();

        if (ids != null) userQuery = userQuery.Where(u => ids.Contains(u.Id));

        var user = await userQuery
            .IncludeUserThumbnails()
            .UseQueryOptions(options)
            .ToListAsync(cancellationToken: ct);

        return user;
    }

    public async Task<List<User>> GetValidUsers(List<int>? ids = null, QueryOptions? options = null, CancellationToken ct = default)
    {
        var baseQuery = dbContext.Users.AsQueryable();

        if (ids != null) baseQuery = baseQuery.Where(u => ids.Contains(u.Id));

        var user = await baseQuery
            .FilterValidUsers()
            .IncludeUserThumbnails()
            .UseQueryOptions(options)
            .ToListAsync(cancellationToken: ct);

        return user;
    }

    public async Task<(List<User> Users, int TotalCount)> GetUsersFriends(User user, QueryOptions? options = null, CancellationToken ct = default)
    {
        var friendsQuery = dbContext.Users
            .Where(u => user.FriendsList.Contains(u.Id))
            .FilterValidUsers();

        var totalCount = options?.IgnoreCountQueryIfExists == false ? await friendsQuery.CountAsync(cancellationToken: ct) : -1;

        var friends = await friendsQuery
            .IncludeUserThumbnails()
            .UseQueryOptions(options)
            .ToListAsync(cancellationToken: ct);

        return (friends, totalCount);
    }

    public async Task<int> CountUsers(CancellationToken ct = default)
    {
        return await dbContext.Users
            .CountAsync(cancellationToken: ct);
    }

    public async Task<int> CountRestrictedUsers(CancellationToken ct = default)
    {
        return await dbContext.Users
            .Where(u => u.AccountStatus == UserAccountStatus.Restricted)
            .CountAsync(cancellationToken: ct);
    }

    public async Task<int> CountValidUsers(CancellationToken ct = default)
    {
        return await dbContext.Users
            .FilterValidUsers()
            .CountAsync(cancellationToken: ct);
    }

    public async Task<Result> UpdateUserUsername(User user, string oldUsername, string newUsername, int? updatedById = null, string? userIp = null)
    {
        return await databaseService.Value.CommitAsTransactionAsync(async () =>
        {
            user.Username = newUsername;

            var updateUserResult = await UpdateUser(user);
            if (updateUserResult.IsFailure)
                throw new Exception(updateUserResult.Error);

            var result = await databaseService.Value.Events.Users.AddUserChangeUsernameEvent(user.Id, userIp ?? "", oldUsername, newUsername, updatedById);
            if (result.IsFailure)
                throw new Exception(result.Error);
        });
    }

    public async Task<Result> UpdateUser(User user)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            dbContext.UpdateEntity(user);
            await dbContext.SaveChangesAsync();
        });
    }

    public async Task<Result> DeleteUser(int userId)
    {
        return await databaseService.Value.CommitAsTransactionAsync(async () =>
        {
            var user = await GetUser(id: userId);
            if (user == null)
                throw new ApplicationException(QueryResultError.REQUESTED_RECORD_NOT_FOUND);

            var isUserHasAnyLoginEvents = await databaseService.Value.Events.Users.IsUserHasAnyLoginEvents(user.Id);
            var isUserHasAnyScore = await databaseService.Value.Scores.GetUserLastScore(userId) != null;

            if (isUserHasAnyLoginEvents || isUserHasAnyScore || user.IsUserSunriseBot())
            {
                _logger.LogWarning($"User {user.Username} has login events or some active score. Deleting user with any of these conditions is not allowed.");
                throw new ApplicationException(QueryResultError.CANT_REMOVE_REQUESTED_RECORD);
            }

            dbContext.Users.Remove(user);
            await dbContext.SaveChangesAsync();
        });
    }

    public async Task<List<User>> GetValidUsersByQueryLike(string queryLike, QueryOptions? options = null, CancellationToken ct = default)
    {
        return await dbContext.Users
            .FilterValidUsers()
            .Where(q => EF.Functions.Like(q.Username, "%" + queryLike + "%"))
            .IncludeUserThumbnails()
            .UseQueryOptions(options)
            .ToListAsync(cancellationToken: ct);
    }
}