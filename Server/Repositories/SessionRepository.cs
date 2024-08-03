using System.Collections.Concurrent;
using HOPEless.Bancho;
using Sunrise.Server.Data;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.Models;
using Sunrise.Server.Objects.Serializable;

namespace Sunrise.Server.Repositories;

public class SessionRepository
{
    private readonly SunriseDb _database;
    private readonly ConcurrentDictionary<int, Session> _sessions = new();

    public SessionRepository(SunriseDb database)
    {
        _database = database;
        const int oneMinute = 60 * 1000;

        Task.Run(async () =>
        {
            while (true)
            {
                ClearInactiveSessions();
                await Task.Delay(oneMinute);
            }
        });
    }

    public void WriteToAllSessions(PacketType type, object data)
    {
        foreach (var session in _sessions.Values)
        {
            session.WritePacket(type, data);
        }
    }

    public Session CreateSession(User user, Location location)
    {
        var session = new Session(user, location, _database);

        _sessions.TryAdd(user.Id, session);
        return session;
    }

    public void RemoveSession(int userId)
    {
        _sessions.TryRemove(userId, out _);
    }

    public Session? GetSession(string token)
    {
        return _sessions.Values.FirstOrDefault(x => x.Token == token);
    }

    public bool IsUserOnline(int userId)
    {
        return _sessions.Values.Any(x => x.User.Id == userId);
    }

    public Session? GetSessionByUserId(int userId)
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

    private void ClearInactiveSessions()
    {
        foreach (var session in _sessions.Values)
        {
            if (session.Attributes.LastPingRequest >= DateTime.UtcNow.AddMinutes(-1))
                continue;

            WriteToAllSessions(PacketType.ServerUserQuit, session.User.Id);
            RemoveSession(session.User.Id);
        }
    }
}