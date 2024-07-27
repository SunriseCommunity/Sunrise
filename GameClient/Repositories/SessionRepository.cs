using System.Collections.Concurrent;
using HOPEless.Bancho;
using Sunrise.Database.Schemas;
using Sunrise.GameClient.Objects;
using Sunrise.GameClient.Objects.Serializable;

namespace Sunrise.GameClient.Repositories;

public class SessionRepository
{
    private readonly ConcurrentDictionary<int, Session> _sessions = new ConcurrentDictionary<int, Session>();

    internal void WriteToAllSessions(PacketType type, object data)
    {
        foreach (var session in _sessions.Values)
            session.WritePacket(type, data);
    }

    public Session CreateSession(User user, Location location)
    {
        var session = new Session(user, location);

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

    public void SendCurrentPlayers(Session session)
    {
        var players = _sessions.Values.Where(x => x.User.Id != session.User.Id).ToList();

        foreach (var player in players)
        {
            session.WritePacket(PacketType.ServerUserPresence, player.Attributes.GetPlayerPresence());
            session.WritePacket(PacketType.ServerUserData, player.Attributes.GetPlayerData());
        }
    }

}