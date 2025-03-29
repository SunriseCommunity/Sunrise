using System.Collections.Concurrent;
using Hangfire;
using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using HOPEless.osu;
using Microsoft.Extensions.DependencyInjection;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Shared.Repositories;

public class SessionRepository
{
    private readonly ChatChannelRepository _channels;
    private readonly ConcurrentDictionary<string, Session> _sessions = new();

    public SessionRepository(ChatChannelRepository channels)
    {
        _channels = channels;

        RecurringJob.AddOrUpdate("ClearInactiveSessions", () => ClearInactiveSessions(), "*/1 * * * *");
    }

    public void WriteToAllSessions(PacketType type, object data, int ignoreUserId = -1)
    {
        foreach (var session in _sessions.Values)
        {
            if (session.UserId == ignoreUserId)
                continue;

            session.WritePacket(type, data);
        }
    }

    public Session CreateSession(User user, Location location, LoginRequest loginRequest)
    {
        var session = new Session(user, location, loginRequest)
        {
            Attributes =
            {
                IgnoreNonFriendPm = loginRequest.BlockNonFriendPm,
                ShowUserLocation = loginRequest.ShowCityLocation
            }
        };

        _sessions.TryAdd(session.Token, session);
        return session;
    }

    /*
     * Soft remove current session from chats, multiplayer and spectating.
     * While not removing it, so on request we could find current session and send LoginReply
     */
    public async Task SoftRemoveSession(Session session)
    {
        session.Match?.RemovePlayer(session);

        session.Spectating?.RemoveSpectator(session);
        session.Spectating = null;

        foreach (var channel in _channels.GetChannels())
        {
            channel.RemoveUser(session.UserId);
        }

        session.WritePacket(PacketType.ServerLoginReply, (int)LoginResponse.InvalidCredentials);

        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var user = await database.Users.GetUser(id: session.UserId);
        if (user == null)
            return;

        user.LastOnlineTime = DateTime.UtcNow;
        await database.Users.UpdateUser(user);
    }

    public async Task RemoveSession(Session session)
    {
        await SoftRemoveSession(session);

        _sessions.TryRemove(session.Token, out _);
    }

    public bool TryGetSession(string username, string? passhash, out Session? session)
    {
        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var searchedUser = database.Users.GetUser(username: username, passhash: passhash, options: new QueryOptions(true))
            .ConfigureAwait(false).GetAwaiter().GetResult();

        if (searchedUser == null)
        {
            session = null;
            return false;
        }

        session = _sessions.Values.FirstOrDefault(x => x.UserId == searchedUser.Id);
        return session != null;
    }

    public bool TryGetSession(out Session? session, string? token = null, long? userId = null)
    {
        session = _sessions.Values.FirstOrDefault(x => x.Token == token || x.UserId == userId);
        return session != null;
    }

    public Session? GetSession(string? username = null, string? token = null, long? userId = null)
    {
        Session? session = null;

        if (username != null)
            TryGetSession(username, null, out session);

        if (token != null || userId != null)
            TryGetSession(out session, token, userId);

        return session;
    }

    public bool IsUserOnline(int userId)
    {
        return _sessions.Values.Any(x => x.UserId == userId);
    }

    public async Task SendCurrentPlayers(Session session)
    {
        var players = _sessions.Values.Where(x => x.UserId != session.UserId).ToList();

        foreach (var player in players)
        {
            session.WritePacket(PacketType.ServerUserPresence, await player.Attributes.GetPlayerPresence());
            session.WritePacket(PacketType.ServerUserData, await player.Attributes.GetPlayerData());
        }
    }

    public List<Session> GetSessions()
    {
        return _sessions.Values.ToList();
    }

    public async Task AddBotToSession()
    {
        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var bot = await database.Users.GetUser(username: Configuration.BotUsername);

        if (bot == null)
            throw new Exception("Bot not found in the database while initializing bot in the session repository.");

        var loginRequest = new LoginRequest(
            Configuration.BotUsername,
            "Hash",
            "Version",
            0,
            false,
            "Hash",
            false
        );

        var session = new Session(bot, new Location(), loginRequest)
        {
            Attributes =
            {
                IsBot = true,
                ShowUserLocation = false,
                UsesOsuClient = false,

                Status = new BanchoUserStatus
                {
                    Action = BanchoAction.Unknown
                }
            }
        };

        _sessions.TryAdd(session.Token, session);
    }

    public async Task ClearInactiveSessions()
    {
        foreach (var session in _sessions.Values)
        {
            if (session.Attributes.LastPingRequest >= DateTime.UtcNow.AddMinutes(-1) || session.Attributes.IsBot)
                continue;

            WriteToAllSessions(PacketType.ServerUserQuit, session.UserId);
            await RemoveSession(session);
        }
    }
}