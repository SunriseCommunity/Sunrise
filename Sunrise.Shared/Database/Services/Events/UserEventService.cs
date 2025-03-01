using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models.Events;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Utils;

namespace Sunrise.Shared.Database.Services;

public class UserEventService
{
    private readonly DatabaseService _databaseService;
    private readonly SunriseDbContext _dbContext;


    public UserEventService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
        _dbContext = databaseService.DbContext;
    }

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

            _dbContext.EventUsers.Add(loginEvent);
            await _dbContext.SaveChangesAsync();
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

            _dbContext.EventUsers.Add(registerEvent);
            await _dbContext.SaveChangesAsync();
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

            _dbContext.EventUsers.Add(changePasswordEvent);
            await _dbContext.SaveChangesAsync();
        });
    }

    public async Task<EventUser?> GetLastUsernameChangeEvent(int userId, QueryOptions? options = null)
    {
        return await _dbContext.EventUsers
            .Where(x => x.UserId == userId && x.EventType == UserEventType.ChangeUsername)
            .OrderByDescending(x => x.Id)
            .UseQueryOptions(options)
            .FirstOrDefaultAsync();
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

            _dbContext.EventUsers.Add(changeUsernameEvent);
            await _dbContext.SaveChangesAsync();
        });
    }

    public async Task<bool> IsUserHasAnyLoginEvents(int userId)
    {
        return await _dbContext.EventUsers
            .AsNoTracking()
            .AnyAsync(x => x.UserId == userId && (x.EventType == UserEventType.GameLogin || x.EventType == UserEventType.WebLogin));
    }

    public async Task<bool> IsIpHasAnyRegisterEvents(string ip)
    {
        return await _dbContext.EventUsers
            .AsNoTracking()
            .AnyAsync(x => x.Ip == ip && x.EventType == UserEventType.Register);
    }
}