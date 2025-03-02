using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Database.Services.Users;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Services;
using Sunrise.Shared.Utils;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.Shared.Database.Repositories;

public class UserRepository
{
    private readonly DatabaseService _databaseService;
    private readonly SunriseDbContext _dbContext;

    private readonly ILogger _logger;

    public UserRepository(DatabaseService databaseService, SessionRepository sessions, CalculatorService calculatorService)
    {
        var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        _logger = loggerFactory.CreateLogger<UserRepository>();

        _databaseService = databaseService;
        _dbContext = databaseService.DbContext;

        Stats = new UserStatsService(_databaseService);
        Moderation = new UserModerationService(_databaseService, sessions, calculatorService);
        Medals = new UserMedalsService(_databaseService);
        Favourites = new UserFavouritesService(_databaseService);
        Files = new UserFileService(_databaseService);
    }

    public UserStatsService Stats { get; }
    public UserFavouritesService Favourites { get; }
    public UserMedalsService Medals { get; }
    public UserModerationService Moderation { get; }
    public UserFileService Files { get; }

    public async Task<Result> AddUser(User user)
    {
        return await _databaseService.CommitAsTransactionAsync(async () =>
        {
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

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

            await _dbContext.SaveChangesAsync();
        });
    }

    public async Task<User?> GetUser(
        long? id = null,
        string? username = null,
        string? email = null,
        string? passhash = null,
        QueryOptions? options = null
    )
    {
        if (passhash != null && id == null && username == null && email == null)
            throw new ArgumentException("Passhash provided without any other parameters");

        var userQuery = _dbContext.Users.AsQueryable();

        if (id != null) userQuery = userQuery.Where(u => u.Id == id);
        if (username != null) userQuery = userQuery.Where(u => u.Username == username);
        if (email != null) userQuery = userQuery.Where(u => u.Email == email);
        if (passhash != null) userQuery = userQuery.Where(u => u.Passhash == passhash);

        var user = await userQuery
            .UseQueryOptions(options)
            .FirstOrDefaultAsync();

        return user;
    }


    public async Task<User?> GetValidUser(
        int? id = null,
        string? username = null,
        string? email = null,
        string? passhash = null,
        QueryOptions? options = null
    )
    {
        if (passhash != null && id == null && username == null && email == null)
            throw new ArgumentException("Passhash provided without any other parameters");

        var userQuery = _dbContext.Users.AsQueryable();

        if (id != null) userQuery = userQuery.Where(u => u.Id == id);
        if (username != null) userQuery = userQuery.Where(u => u.Username == username);
        if (email != null) userQuery = userQuery.Where(u => u.Email == email);
        if (passhash != null) userQuery = userQuery.Where(u => u.Passhash == passhash);

        var user = await userQuery
            .FilterValidUsers()
            .UseQueryOptions(options)
            .FirstOrDefaultAsync();

        return user;
    }

    public async Task<List<User>> GetUsers(List<int>? ids = null, QueryOptions? options = null)
    {
        var userQuery = _dbContext.Users.AsQueryable();

        if (ids != null) userQuery = userQuery.Where(u => ids.Contains(u.Id));

        var user = await userQuery
            .UseQueryOptions(options)
            .ToListAsync();

        return user;
    }

    public async Task<List<User>> GetValidUsers(List<int>? ids = null, QueryOptions? options = null)
    {
        var baseQuery = _dbContext.Users.AsQueryable();

        if (ids != null) baseQuery = baseQuery.Where(u => ids.Contains(u.Id));

        var user = await baseQuery
            .FilterValidUsers()
            .UseQueryOptions(options)
            .ToListAsync();

        return user;
    }


    public async Task<int> CountUsers()
    {
        return await _dbContext.Users
            .CountAsync();
    }

    public async Task<int> CountValidUsers()
    {
        return await _dbContext.Users
            .FilterValidUsers()
            .CountAsync();
    }

    public async Task<Result> UpdateUserUsername(User user, string oldUsername, string newUsername, int? updatedById = null, string? userIp = null)
    {
        return await _databaseService.CommitAsTransactionAsync(async () =>
        {
            user.Username = newUsername;

            var updateUserResult = await UpdateUser(user);
            if (updateUserResult.IsFailure)
                throw new Exception(updateUserResult.Error);

            var result = await _databaseService.Events.Users.AddUserChangeUsernameEvent(user.Id, userIp ?? "", oldUsername, newUsername, updatedById);
            if (result.IsFailure)
                throw new Exception(result.Error);
        });
    }

    public async Task<Result> UpdateUser(User user)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            _dbContext.UpdateEntity(user);
            await _dbContext.SaveChangesAsync();
        });
    }

    public async Task<Result> DeleteUser(int userId)
    {
        return await _databaseService.CommitAsTransactionAsync(async () =>
        {
            var user = await GetUser(id: userId);
            if (user == null)
                throw new ApplicationException(QueryResultError.REQUESTED_RECORD_NOT_FOUND);

            var isUserHasAnyLoginEvents = await _databaseService.Events.Users.IsUserHasAnyLoginEvents(user.Id);
            var isUserHasAnyScore = await _databaseService.Scores.GetUserLastScore(userId) != null;

            if (isUserHasAnyLoginEvents || isUserHasAnyScore || user.IsUserSunriseBot())
            {
                _logger.LogWarning($"User {user.Username} has login events or some active score. Deleting user with any of these conditions is not allowed.");
                throw new ApplicationException(QueryResultError.CANT_REMOVE_REQUESTED_RECORD);
            }

            _dbContext.Users.Remove(user);
            await _dbContext.SaveChangesAsync();
        });
    }

    public async Task<List<User>> GetValidUsersByQueryLike(string queryLike, QueryOptions? options = null)
    {
        return await _dbContext.Users
            .FilterValidUsers()
            .Where(q => q.Username.Contains(queryLike))
            .UseQueryOptions(options)
            .ToListAsync();
    }
}