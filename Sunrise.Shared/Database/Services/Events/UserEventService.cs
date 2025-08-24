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
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var loginEvent = new EventUser
            {
                EventType = isFromGame ? UserEventType.GameLogin : UserEventType.WebLogin,
                UserId = userId,
                Ip = ip
            };

            loginEvent.SetData(new
            {
                LoginData = loginData
            });

            dbContext.EventUsers.Add(loginEvent);
            await dbContext.SaveChangesAsync();
        });
    }

    public async Task<Result> AddUserRegisterEvent(int userId, string ip, User userData)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var registerEvent = new EventUser
            {
                EventType = UserEventType.Register,
                UserId = userId,
                Ip = ip
            };

            registerEvent.SetData(new
            {
                RegisterData = new
                {
                    userData.Username,
                    userData.Email,
                    userData.Passhash,
                    userData.Country,
                    userData.RegisterDate
                }
            });

            dbContext.EventUsers.Add(registerEvent);
            await dbContext.SaveChangesAsync();
        });
    }

    public async Task<Result> AddUserChangePasswordEvent(int userId, string ip, string oldPassword, string newPassword)
    {
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var changePasswordEvent = new EventUser
            {
                EventType = UserEventType.ChangePassword,
                UserId = userId,
                Ip = ip
            };

            changePasswordEvent.SetData(new
            {
                OldPasswordHash = oldPassword,
                NewPasswordHash = newPassword
            });

            dbContext.EventUsers.Add(changePasswordEvent);
            await dbContext.SaveChangesAsync();
        });
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
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var changeUsernameEvent = new EventUser
            {
                EventType = UserEventType.ChangeUsername,
                UserId = userId,
                Ip = ip
            };

            changeUsernameEvent.SetData(new
            {
                OldUsername = oldUsername,
                NewUsername = newUsername,
                UpdatedById = updatedById
            });

            dbContext.EventUsers.Add(changeUsernameEvent);
            await dbContext.SaveChangesAsync();
        });
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
        return await ResultUtil.TryExecuteAsync(async () =>
        {
            var changeCountryEvent = new EventUser
            {
                EventType = UserEventType.ChangeCountry,
                UserId = userId,
                Ip = ip
            };

            changeCountryEvent.SetData(new
            {
                NewCountry = newCountry,
                OldCountry = oldCountry,
                UpdatedById = updatedById
            });


            dbContext.EventUsers.Add(changeCountryEvent);
            await dbContext.SaveChangesAsync();
        });
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
}