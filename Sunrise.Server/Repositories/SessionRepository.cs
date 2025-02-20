using System.Collections.Concurrent;
using Hangfire;
using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using HOPEless.osu;
using Sunrise.Server.Objects;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Models.User;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Types.Enums;
using Sunrise.Shared.Types.Interfaces;
using ISession = Sunrise.Shared.Types.Interfaces.ISession;

namespace Sunrise.Server.Repositories;

public class SessionRepository : ISessionRepository
{
    private readonly ChannelRepository _channels;
    private readonly ConcurrentDictionary<string, ISession> _sessions = new();

    public SessionRepository(ChannelRepository channels)
    {
        _channels = channels;

        AddBotToSession();

        RecurringJob.AddOrUpdate("ClearInactiveSessions", () => ClearInactiveSessions(), "*/1 * * * *");
    }

    public void WriteToAllSessions(PacketType type, object data, int ignoreUserId = -1)
    {
        foreach (var session in _sessions.Values)
        {
            if (session.User.Id == ignoreUserId)
                continue;

            session.WritePacket(type, data);
        }
    }

    /*
     * Soft remove current session from chats, multiplayer and spectating.
     * While not removing it, so on request we could find current session and send LoginReply
     */
    public void SoftRemoveSession(ISession session)
    {
        session.Match?.RemovePlayer(session);

        session.Spectating?.RemoveSpectator(session);
        session.Spectating = null;

        foreach (var channel in _channels.GetChannels())
        {
            channel.RemoveUser(session.User.Id);
        }

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        session.User.LastOnlineTime = DateTime.UtcNow;
        _ = database.UserService.UpdateUser(session.User);

        session.WritePacket(PacketType.ServerLoginReply, (int)LoginResponses.InvalidCredentials);
    }

    public void RemoveSession(ISession session)
    {
        SoftRemoveSession(session);

        _sessions.TryRemove(session.Token, out _);
    }


    public bool TryGetSession(string username, string passhash, out ISession? session)
    {
        session = _sessions.Values.FirstOrDefault(x => x.User.Username == username && x.User.Passhash == passhash);
        return session != null;
    }

    public bool TryGetSession(out ISession? session, string? username = null, string? token = null, int? userId = null)
    {
        session = _sessions.Values.FirstOrDefault(x =>
            x.Token == token || x.User.Username == username || x.User.Id == userId);
        return session != null;
    }

    public ISession? GetSession(string? username = null, string? token = null, int? userId = null)
    {
        return TryGetSession(out var session, username, token, userId) ? session : null;
    }

    public bool IsUserOnline(int userId)
    {
        return _sessions.Values.Any(x => x.User.Id == userId);
    }

    public async Task SendCurrentPlayers(ISession session)
    {
        var players = _sessions.Values.Where(x => x.User.Id != session.User.Id).ToList();

        foreach (var player in players)
        {
            session.WritePacket(PacketType.ServerUserPresence, await player.Attributes.GetPlayerPresence());
            session.WritePacket(PacketType.ServerUserData, await player.Attributes.GetPlayerData());
        }
    }

    public List<ISession> GetSessions()
    {
        return _sessions.Values.ToList();
    }

    public void ClearInactiveSessions()
    {
        foreach (var session in _sessions.Values)
        {
            if (session.Attributes.LastPingRequest >= DateTime.UtcNow.AddMinutes(-1) || session.Attributes.IsBot)
                continue;

            WriteToAllSessions(PacketType.ServerUserQuit, session.User.Id);
            RemoveSession(session);
        }
    }

    public ISession CreateSession(User user, Location location, ILoginRequest loginRequest)
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

    private async void AddBotToSession()
    {
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var bot = await database.UserService.GetUser(username: Configuration.BotUsername);

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
}