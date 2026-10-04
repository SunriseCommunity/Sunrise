using System.Collections.Concurrent;
using Hangfire;
using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Shared.Objects.Multiplayer;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Shared.Repositories.Multiplayer;

public class MatchRepository
{
    private readonly ConcurrentQueue<(int id, DateTime time)> _freeMatchIds = new();
    private readonly ConcurrentDictionary<int, MultiplayerMatch> _matches = new();
    private readonly TimeSpan _reuseMatchIdDelay = TimeSpan.FromSeconds(10);

    private readonly ConcurrentDictionary<int, Session> _sessionsInLobby = new();

    private int _matchIdCounter;

    public MatchRepository(IRecurringJobManager recurringJobManager)
    {
        recurringJobManager.AddOrUpdate("ClearInactiveMatches", () => ClearInactiveMatches(), Cron.Minutely);
    }

    public IEnumerable<MultiplayerMatch> GetMatches()
    {
        return _matches.Values;
    }

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

    public int GetLobbySessionCount()
    {
        return _sessionsInLobby.Count;
    }

    public bool CreateMatchWithHost(Session session, BanchoMultiplayerMatch match)
    {
        var multiplayerMatch = new MultiplayerMatch(this, match)
        {
            Match =
            {
                MatchId = GetNextMatchId()
            }
        };

        _matches.TryAdd(match.MatchId, multiplayerMatch);

        if (!multiplayerMatch.TryAddPlayer(session))
        {
            _matches.TryRemove(match.MatchId, out _);
            _freeMatchIds.Enqueue((match.MatchId, DateTime.MinValue)); // We didn't announce this match to lobby, so we can reuse this match id immediately
            return false;
        }

        WriteUpdateToLobby(multiplayerMatch, true);
        return true;
    }

    public bool TryJoinMatch(Session session, BanchoMultiplayerJoin joinData)
    {
        if (!_matches.TryGetValue(joinData.MatchId, out var multiplayerMatch))
        {
            session.SendMultiMatchJoinFail();
            return false;
        }

        if (multiplayerMatch.Match.GamePassword != null && multiplayerMatch.Match.GamePassword != joinData.Password.Replace(" ", "_"))
        {
            session.SendMultiMatchJoinFail();
            return false;
        }

        if (!multiplayerMatch.TryAddPlayer(session))
        {
            // TryAddPlayer sends the SendMultiMatchJoinFail packet if the join fails, so we don't need to handle non-successful joins here
            return false;
        }

        return true;
    }

    public void UpdateMatch(Session session, BanchoMultiplayerMatch match)
    {
        if (!_matches.TryGetValue(match.MatchId, out var multiplayerMatch))
            return;

        multiplayerMatch.UpdateMatchSettings(match, session);
    }

    public void RemoveMatch(int matchId, bool discardReuseMatchIdDelay = false)
    {
        if (!_matches.TryGetValue(matchId, out var match))
            return;

        if (match.Match.InProgress)
            match.EndGame(true);

        foreach (var session in match.Players.Values)
        {
            match.RemovePlayer(session, true);
        }

        _matches.TryRemove(matchId, out _);
        WriteRemoveToLobby(matchId);
        _freeMatchIds.Enqueue((matchId, discardReuseMatchIdDelay ? DateTime.MinValue : DateTime.UtcNow));
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

    public int GetMatchCount()
    {
        return _matches.Count;
    }

    private int GetNextMatchId()
    {
        while (_freeMatchIds.TryPeek(out var entry))
        {
            // If user will send request to join match X, which is deleted in the middle of the request and replaced with match Y. X.MatchId shouldn't be == to Y.MatchId
            if (DateTime.UtcNow - entry.time < _reuseMatchIdDelay)
                break;

            if (_freeMatchIds.TryDequeue(out entry))
                return entry.id;
        }

        return Interlocked.Increment(ref _matchIdCounter);
    }

    public void ClearInactiveMatches()
    {
        _matches.Where(x => x.Value.CreatedAt < DateTime.UtcNow - TimeSpan.FromMinutes(1) && x.Value.Players.IsEmpty)
            .ToList()
            .ForEach(x => RemoveMatch(x.Key));
    }
}