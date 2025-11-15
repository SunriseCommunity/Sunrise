using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models.Events;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects;
using Sunrise.Shared.Objects.Serializable.Events;
using Sunrise.Shared.Utils;

namespace Sunrise.Shared.Database.Services.Events;

public class UserEventService(SunriseDbContext dbContext)
{
    public async Task<Result> AddUserLoginEvent(UserEventAction userEventAction, bool isFromGame, object loginData)
    {
        var data = new
        {
            LoginData = loginData
        };

        return await AddUserEvent(isFromGame ? UserEventType.GameLogin : UserEventType.WebLogin, userEventAction, data);
    }

    public async Task<Result> AddUserRegisterEvent(UserEventAction userEventAction, User userData)
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

        return await AddUserEvent(UserEventType.Register, userEventAction, data);
    }

    public async Task<Result> AddUserChangePasswordEvent(UserEventAction userEventAction, string oldPassword, string newPassword)
    {
        var data = new
        {
            OldPasswordHash = oldPassword,
            NewPasswordHash = newPassword,
            UpdatedById = userEventAction.ExecutorUser.Id
        };

        return await AddUserEvent(UserEventType.ChangePassword, userEventAction, data);
    }

    public async Task<Result> AddUserChangeBannerEvent(UserEventAction userEventAction, string oldBannerHash, string newBannerHash)
    {
        var data = new
        {
            OldBannerHash = oldBannerHash,
            NewBannerHash = newBannerHash,
            UpdatedById = userEventAction.ExecutorUser.Id
        };

        return await AddUserEvent(UserEventType.ChangeBanner, userEventAction, data);
    }

    public async Task<Result> AddUserChangeAvatarEvent(UserEventAction userEventAction, string oldAvatarHash, string newAvatarHash)
    {
        var data = new
        {
            OldAvatarHash = oldAvatarHash,
            NewAvatarHash = newAvatarHash,
            UpdatedById = userEventAction.ExecutorUser.Id
        };

        return await AddUserEvent(UserEventType.ChangeAvatar, userEventAction, data);
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

    public async Task<Result> AddUserChangeUsernameEvent(UserEventAction userEventAction, string oldUsername, string newUsername)
    {
        var data = new
        {
            OldUsername = oldUsername,
            NewUsername = newUsername,
            UpdatedById = userEventAction.ExecutorUser.Id
        };

        return await AddUserEvent(UserEventType.ChangeUsername, userEventAction, data);
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

    public async Task<Result> AddUserChangeCountryEvent(UserEventAction userEventAction, CountryCode oldCountry, CountryCode newCountry)
    {
        var data = new
        {
            NewCountry = newCountry,
            OldCountry = oldCountry,
            UpdatedById = userEventAction.ExecutorUser.Id
        };

        return await AddUserEvent(UserEventType.ChangeCountry, userEventAction, data);
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

    private async Task<Result> AddUserEvent<T>(UserEventType eventType, UserEventAction userEventAction, T data)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var newEvent = new EventUser
            {
                EventType = eventType,
                UserId = userEventAction.TargetUserId,
                Ip = userEventAction.ExecutorIp
                // TODO: Move executor ID here from data object
            };

            newEvent.SetData(data);

            dbContext.EventUsers.Add(newEvent);
            await dbContext.SaveChangesAsync();
        });
    }

    public async Task<(int, List<EventUser>)> GetUserEvents(int userId, QueryOptions? options = null, CancellationToken ct = default)
    {
        var query = dbContext.EventUsers
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.Id);

        var totalCount = options?.IgnoreCountQueryIfExists == true ? -1 : await query.CountAsync(cancellationToken: ct);

        var result = await query
            .UseQueryOptions(options)
            .ToListAsync(cancellationToken: ct);

        return (totalCount, result);
    }
}