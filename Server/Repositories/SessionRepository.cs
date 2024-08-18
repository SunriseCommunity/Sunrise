using System.Collections.Concurrent;
using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using HOPEless.osu;
using osu.Shared;
using Sunrise.Server.Data;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.Models;
using Sunrise.Server.Objects.Serializable;
using Sunrise.Server.Repositories.Chat;
using Sunrise.Server.Types.Enums;
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

    public Session? GetSession(string? token = null, string? username = null)
    {
        return _sessions.Values.FirstOrDefault(x => x.Token == token || x.User.Username == username);
    }

    public bool IsUserOnline(int userId)
    {
        return _sessions.Values.Any(x => x.User.Id == userId);
    }

    public Session? GetSession(int userId)
    {
        return _sessions.Values.FirstOrDefault(x => x.User.Id == userId);
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

    private void AddBotToSession()
    {
        // TODO: On a side not, it's better to add the bot to the database and then retrieve it from there. Will do that later.

        var bot = new User
        {
            Id = int.MaxValue,
            Username = Configuration.BotUsername,
            Country = (short)CountryCodes.AQ, // Antarctica, because our bot is "cool" :D
            Privilege = PlayerRank.SuperMod,
            RegisterDate = DateTime.Now
        };

        var session = new Session(bot, new Location(), _database)
        {
            Attributes =
            {
                IsBot = true,
                ShowUserLocation = false,
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
            if (session.Attributes.LastPingRequest >= DateTime.UtcNow.AddMinutes(-1))
                continue;

            if (session.Attributes.IsBot)
                continue;

            WriteToAllSessions(PacketType.ServerUserQuit, session.User.Id);
            RemoveSession(session.User.Id);
        }
    }
}