using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models.Events;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects.Serializable.Events;
using Sunrise.Shared.Utils;

namespace Sunrise.Shared.Database.Services.Events;

public class UserEventService(SunriseDbContext dbContext)
{
    public async Task<Result> AddUserLoginEvent(int userId, string ip, bool isFromGame, object loginData)
    {
        var data = new
        {
            LoginData = loginData
        };

        return await AddUserEvent(isFromGame ? UserEventType.GameLogin : UserEventType.WebLogin, userId, ip, data);
    }

    public async Task<Result> AddUserRegisterEvent(int userId, string ip, User userData)
    {
        var data = new
        {
            RegisterData = new
            {
                userData.Username,
                userData.Email,
                userData.Passhash,
                userData.Country,
                userData.RegisterDate
            }
        };

        return await AddUserEvent(UserEventType.Register, userId, ip, data);
    }

    public async Task<Result> AddUserChangePasswordEvent(int userId, string ip, string oldPassword, string newPassword, int? updatedById = null)
    {
        var data = new
        {
            OldPasswordHash = oldPassword,
            NewPasswordHash = newPassword,
            UpdatedById = updatedById
        };

        return await AddUserEvent(UserEventType.ChangePassword, userId, ip, data);
    }

    public async Task<EventUser?> GetLastUsernameChangeEvent(int userId, QueryOptions? options = null, CancellationToken ct = default)
    {
        return await dbContext.EventUsers
            .Where(x => x.UserId == userId && x.EventType == UserEventType.ChangeUsername)
            .OrderByDescending(x => x.Id)
            .UseQueryOptions(options)
            .FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<List<EventUser>> GetUserPreviousUsernameChangeEvents(int userId, QueryOptions? options = null, CancellationToken ct = default)
    {
        return await dbContext.EventUsers
            .Where(x => x.UserId == userId && x.EventType == UserEventType.ChangeUsername)
            .OrderByDescending(x => x.Id)
            .UseQueryOptions(options)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<Result> AddUserChangeUsernameEvent(int userId, string ip, string oldUsername, string newUsername, int? updatedById = null)
    {
        var data = new
        {
            OldUsername = oldUsername,
            NewUsername = newUsername,
            UpdatedById = updatedById
        };

        return await AddUserEvent(UserEventType.ChangeUsername, userId, ip, data);
    }

    public async Task<Result> SetUserChangeUsernameEventVisibility(int id, bool hidden, CancellationToken ct = default)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var changeUsernameEvent = await dbContext.EventUsers
                .FirstOrDefaultAsync(x => x.Id == id && x.EventType == UserEventType.ChangeUsername, ct);

            if (changeUsernameEvent == null)
                throw new Exception("Username change event not found");

            var previousData = changeUsernameEvent.GetData<UserUsernameChanged>();

            changeUsernameEvent.SetData(new
            {
                previousData?.OldUsername,
                previousData?.NewUsername,
                previousData?.UpdatedById,
                IsHiddenFromPreviousUsernames = hidden
            });

            await dbContext.SaveChangesAsync(ct);
        });
    }

    public async Task<bool> IsUserHasAnyLoginEvents(int userId, CancellationToken ct = default)
    {
        return await dbContext.EventUsers
            .AsNoTracking()
            .AnyAsync(x => x.UserId == userId && (x.EventType == UserEventType.GameLogin || x.EventType == UserEventType.WebLogin), ct);
    }

    public async Task<User?> IsIpHasAnyRegisteredAccounts(string ip, CancellationToken ct = default)
    {
        var userEvent = await dbContext.EventUsers
            .AsNoTracking().Include(eventUser => eventUser.User)
            .FirstOrDefaultAsync(x => x.Ip == ip && x.EventType == UserEventType.Register, ct);

        return userEvent?.User;
    }

    public async Task<Result> AddUserChangeCountryEvent(int userId, CountryCode oldCountry, CountryCode newCountry, string ip, int? updatedById)
    {
        var data = new
        {
            NewCountry = newCountry,
            OldCountry = oldCountry,
            UpdatedById = updatedById
        };

        return await AddUserEvent(UserEventType.ChangeCountry, userId, ip, data);
    }

    public async Task<EventUser?> GetLastUserCountryChangeEvent(int userId, QueryOptions? options = null,
        CancellationToken ct = default)
    {
        return await dbContext.EventUsers
            .Where(x => x.UserId == userId && x.EventType == UserEventType.ChangeCountry)
            .OrderByDescending(x => x.Id)
            .UseQueryOptions(options)
            .FirstOrDefaultAsync(cancellationToken: ct);
    }

    private async Task<Result> AddUserEvent<T>(UserEventType eventType, int userId, string ip, T data)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var newEvent = new EventUser
            {
                EventType = eventType,
                UserId = userId,
                Ip = ip
            };

            newEvent.SetData(data);

            dbContext.EventUsers.Add(newEvent);
            await dbContext.SaveChangesAsync();
        });
    }
}