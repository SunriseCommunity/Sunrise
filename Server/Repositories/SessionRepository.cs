using System.Collections.Concurrent;
using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using HOPEless.osu;
using Sunrise.Server.Data;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.Models;
using Sunrise.Server.Objects.Serializable;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Repositories;

public class SessionRepository
{
    private readonly ChannelRepository _channels;
    private readonly SunriseDb _database;
    private readonly ConcurrentDictionary<int, Session> _sessions = new();

    public SessionRepository(SunriseDb database, ChannelRepository channels)
    {
        _database = database;
        _channels = channels;

        AddBotToSession();

        const int second = 1000;
        Task.Run(async () =>
        {
            while (true)
            {
                ClearInactiveSessions();
                await Task.Delay(60 * second);
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
        var session = new Session(user, location, _database)
        {
            Attributes =
            {
                IgnoreNonFriendPm = loginRequest.BlockNonFriendPm,
                ShowUserLocation = loginRequest.ShowCityLocation
            }
        };

        _sessions.TryAdd(user.Id, session);
        return session;
    }

    public void RemoveSession(int userId)
    {
        foreach (var channel in _channels.GetChannels())
        {
            channel.RemoveUser(userId);
        }

        _sessions.TryRemove(userId, out _);
    }


    public bool TryGetSession(string username, string passhash, out Session? session)
    {
        session = _sessions.Values.FirstOrDefault(x => x.User.Username == username && x.User.Passhash == passhash);
        return session != null;
    }

    public bool TryGetSession(out Session? session, string? username = null, string? token = null, int? userId = null)
    {
        session = _sessions.Values.FirstOrDefault(x => x.Token == token || x.User.Username == username || x.User.Id == userId);
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
        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();

        var bot = await database.GetUser(username: Configuration.BotUsername);

        if (bot == null)
        {
            throw new Exception("Bot not found in the database while initializing bot in the session repository.");
        }

        var session = new Session(bot, new Location(), _database)
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

        _sessions.TryAdd(bot.Id, session);
    }

    private void ClearInactiveSessions()
    {
        foreach (var session in _sessions.Values)
        {
            if (session.Attributes.LastPingRequest >= DateTime.UtcNow.AddMinutes(-1) || session.Attributes.IsBot)
                continue;

            WriteToAllSessions(PacketType.ServerUserQuit, session.User.Id);
            RemoveSession(session.User.Id);
        }
    }
}