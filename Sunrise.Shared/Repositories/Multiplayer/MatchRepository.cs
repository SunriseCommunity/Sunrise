using System.Collections.Concurrent;
using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Shared.Objects.Multiplayer;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Shared.Repositories.Multiplayer;

public class MatchRepository
{
    private readonly ConcurrentDictionary<int, MultiplayerMatch> _matches = new();
    private readonly ConcurrentDictionary<int, Session> _sessionsInLobby = new();
    private short _matchIdCounter = 1;

    public void JoinLobby(Session session)
    {
        _sessionsInLobby.TryAdd(session.UserId, session);

        foreach (var match in _matches.Values)
        {
            session.WritePacket(PacketType.ServerMultiMatchNew, match.Match);
        }
    }

    public void LeaveLobby(Session session)
    {
        _sessionsInLobby.TryRemove(session.UserId, out _);
    }

    public void CreateMatch(BanchoMultiplayerMatch match)
    {
        var multiplayerMatch = new MultiplayerMatch(this, match)
        {
            Match =
            {
                MatchId = _matchIdCounter++
            }
        };

        _matches.TryAdd(match.MatchId, multiplayerMatch);
        WriteUpdateToLobby(multiplayerMatch, true);
    }

    public void JoinMatch(Session session, BanchoMultiplayerJoin joinData)
    {
        if (!_matches.TryGetValue(joinData.MatchId, out var multiplayerMatch))
        {
            session.SendMultiMatchJoinFail();
            return;
        }

        if (multiplayerMatch.Match.GamePassword != null && multiplayerMatch.Match.GamePassword != joinData.Password.Replace(" ", "_"))
        {
            session.SendMultiMatchJoinFail();
            return;
        }

        multiplayerMatch.AddPlayer(session);
    }

    public void UpdateMatch(Session session, BanchoMultiplayerMatch match)
    {
        if (!_matches.TryGetValue(match.MatchId, out var multiplayerMatch))
            return;

        multiplayerMatch.UpdateMatchSettings(match, session);
    }

    public void RemoveMatch(int matchId)
    {
        _matches.TryRemove(matchId, out _);
        WriteRemoveToLobby(matchId);
    }

    public void WriteUpdateToLobby(MultiplayerMatch match, bool isNewLobby = false)
    {
        foreach (var session in _sessionsInLobby.Values)
        {
            session.WritePacket(isNewLobby ? PacketType.ServerMultiMatchNew : PacketType.ServerMultiMatchUpdate, match.Match);
        }
    }

    private void WriteRemoveToLobby(int matchId)
    {
        foreach (var session in _sessionsInLobby.Values)
        {
            session.WritePacket(PacketType.ServerMultiMatchDelete, matchId);
        }
    }
}