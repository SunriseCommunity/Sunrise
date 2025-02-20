using DatabaseWrapper.Core;
using ExpressionTree;
using Microsoft.Extensions.Logging;
using Sunrise.Shared.Database.Models.Event;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Repositories;
using Watson.ORM.Sqlite;

namespace Sunrise.Shared.Database.Services.Event.Services;

public class UserEventService
{
    private readonly WatsonORM _database;
    private readonly ILogger _logger;
    private readonly RedisRepository _redis;
    private readonly DatabaseManager _services;

    public UserEventService(DatabaseManager services, RedisRepository redis, WatsonORM database)
    {
        var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        _logger = loggerFactory.CreateLogger<UserEventService>();

        _services = services;

        _database = database;
        _redis = redis;
    }

    public async Task CreateNewUserLoginEvent(int userId, string ip, bool isFromGame, object loginData)
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

        await _database.InsertAsync(loginEvent);
    }

    public async Task CreateNewUserRegisterEvent(int userId, string ip, Models.User.User userData)
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

        await _database.InsertAsync(registerEvent);
    }

    public async Task CreateNewUserChangePasswordEvent(int userId, string ip, string oldPassword, string newPassword)
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

        await _database.InsertAsync(changePasswordEvent);
    }


    public async Task<EventUser?> GetLastUsernameChange(int userId)
    {
        var lastUsernameChange = await _database.SelectFirstAsync<EventUser>(new Expr("UserId", OperatorEnum.Equals, userId).PrependAnd(
                new Expr("EventType", OperatorEnum.Equals, (int)UserEventType.ChangeUsername)),
            [
                new ResultOrder("Id", OrderDirectionEnum.Descending)
            ]);

        return lastUsernameChange;
    }

    public async Task CreateNewUserChangeUsernameEvent(int userId, string ip, string oldUsername, string newUsername, int? updatedById = null)
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

        await _database.InsertAsync(changeUsernameEvent);
    }

    public async Task<bool> IsUserHasAnyLoginEvent(int userId)
    {
        var loginEvent = await _database.SelectFirstAsync<EventUser>(new Expr("UserId", OperatorEnum.Equals, userId).PrependAnd(
            new Expr("EventType", OperatorEnum.Equals, (int)UserEventType.GameLogin)
                .PrependOr("EventType", OperatorEnum.Equals, (int)UserEventType.WebLogin)));

        return loginEvent != null;

    }

    public async Task<bool> IsIpCreatedAccountBefore(string ip)
    {
        var registerEvent = await _database.SelectFirstAsync<EventUser>(new Expr("Ip", OperatorEnum.Equals, ip).PrependAnd(
            new Expr("EventType", OperatorEnum.Equals, (int)UserEventType.Register)));

        return registerEvent != null;
    }
}