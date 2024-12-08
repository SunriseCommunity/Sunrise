using System.Collections.Concurrent;
using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using HOPEless.osu;
using Sunrise.Server.Application;
using Sunrise.Server.Database;
using Sunrise.Server.Database.Models.User;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.Serializable;
using Sunrise.Server.Types.Enums;

namespace Sunrise.Server.Repositories;

public class SessionRepository
{
    private const int Second = 1000;
    private readonly ChannelRepository _channels;
    private readonly ConcurrentDictionary<string, Session> _sessions = new();

    public SessionRepository(ChannelRepository channels)
    {
        _channels = channels;

        AddBotToSession();

        Task.Run(async () =>
        {
            while (true)
            {
                ClearInactiveSessions();
                await Task.Delay(60 * Second);
            }
        });
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
    public void SoftRemoveSession(Session session)
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

    public void RemoveSession(Session session)
    {
        SoftRemoveSession(session);

        _sessions.TryRemove(session.Token, out _);
    }


    public bool TryGetSession(string username, string passhash, out Session? session)
    {
        session = _sessions.Values.FirstOrDefault(x => x.User.Username == username && x.User.Passhash == passhash);
        return session != null;
    }

    public bool TryGetSession(out Session? session, string? username = null, string? token = null, int? userId = null)
    {
        session = _sessions.Values.FirstOrDefault(x =>
            x.Token == token || x.User.Username == username || x.User.Id == userId);
        return session != null;
    }

    public Session? GetSession(string? username = null, string? token = null, int? userId = null)
    {
        return TryGetSession(out var session, username, token, userId) ? session : null;
    }

    public bool IsUserOnline(int userId)
    {
        return _sessions.Values.Any(x => x.User.Id == userId);
    }

    public async Task SendCurrentPlayers(Session session)
    {
        var players = _sessions.Values.Where(x => x.User.Id != session.User.Id).ToList();

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

    private void ClearInactiveSessions()
    {
        foreach (var session in _sessions.Values)
        {
            if (session.Attributes.LastPingRequest >= DateTime.UtcNow.AddMinutes(-1) || session.Attributes.IsBot)
                continue;

            WriteToAllSessions(PacketType.ServerUserQuit, session.User.Id);
            RemoveSession(session);
        }
    }
}