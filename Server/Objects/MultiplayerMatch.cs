using System.Collections.Concurrent;
using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using HOPEless.osu;
using osu.Shared;
using Sunrise.Server.Repositories;

namespace Sunrise.Server.Objects;

public class MultiplayerMatch
{
    public MultiplayerMatch(MatchRepository matches, BanchoMultiplayerMatch newMatch)
    {
        Match = newMatch;
        Matches = matches;

        foreach (var (_, index) in Match.SlotId.Select((value, i) => (value, i)))
        {
            var isLocked = Match.MultiSlotStatus[index] == MultiSlotStatus.Locked;
            Slots.TryAdd(index, new MultiplayerSlot(isLocked));
        }
    }

    public BanchoMultiplayerMatch Match { get; private set; }
    private MatchRepository Matches { get; }
    private ConcurrentDictionary<int, Session> Players { get; } = new();
    private ConcurrentDictionary<int, MultiplayerSlot> Slots { get; } = new();

    public void UpdateMatchSettings(BanchoMultiplayerMatch updatedMatch, Session session)
    {
        if (Match.MatchId != updatedMatch.MatchId || Match.HostId != session.User.Id)
            return;

        // Ignore invalid changes (Password cannot be changed with this method)
        updatedMatch.GamePassword = Match.GamePassword;

        if (updatedMatch.SpecialModes != Match.SpecialModes)
        {
            if (updatedMatch.SpecialModes == MultiSpecialModes.FreeMod)
            {
                foreach (var (slot, index) in updatedMatch.SlotId.Select((value, i) => (value, i)))
                {
                    if (slot != -1)
                        updatedMatch.SlotMods[index] = Match.ActiveMods & ~(Mods.DoubleTime | Mods.Nightcore | Mods.HalfTime);
                }

                updatedMatch.ActiveMods &= Mods.DoubleTime | Mods.Nightcore | Mods.HalfTime;
            }
            else
            {
                var hostIndex = Array.IndexOf(updatedMatch.SlotId, updatedMatch.HostId);
                var hostMods = Match.SlotMods[hostIndex];

                updatedMatch.ActiveMods &= Mods.DoubleTime | Mods.Nightcore | Mods.HalfTime;
                updatedMatch.ActiveMods |= hostMods;

                foreach (var (slot, index) in updatedMatch.SlotId.Select((value, i) => (value, i)))
                {
                    if (slot != -1)
                        updatedMatch.SlotMods[index] = Mods.None;
                }
            }
        }

        Match = updatedMatch;
        ApplyNewChanges();
    }

    public void AddPlayer(Session session)
    {
        if (session.Match != null || Players.ContainsKey(session.User.Id))
        {
            session.SendMultiMatchJoinFail();
            return;
        }

        var openSlot = GetSlot(status: MultiSlotStatus.Open);

        if (openSlot == null)
        {
            session.SendMultiMatchJoinFail();
            return;
        }

        Players.TryAdd(session.User.Id, session);

        openSlot.AddPlayer(session.User.Id);

        session.SendMultiMatchJoinSuccess(Match);

        ApplyNewChanges();
    }

    public void RemovePlayer(Session session)
    {
        var slot = GetSlot(userId: session.User.Id);

        if (slot == null)
            return;

        if (slot.UserId == Match.HostId)
        {
            var newHost = Players.Values.FirstOrDefault(player => player.User.Id != Match.HostId);
            Match.HostId = newHost?.User.Id ?? -1;
        }

        slot.RemovePlayer();

        ApplyNewChanges();
    }

    public void StartGame()
    {
        if (Match.InProgress)
            return;

        foreach (var slot in Slots.Values)
        {
            if (slot.UserId == -1)
                continue;

            if (slot.Status == MultiSlotStatus.NoMap)
                return;

            slot.UpdateStatus(MultiSlotStatus.Playing);
        }

        Match.InProgress = true;
        ResetGameStatuses();

        var excludedPlayers = Slots.Values.Where(s => s.UserId == -1).Select(s => s.UserId).ToArray();
        WriteToAllPlayers(PacketType.ServerMultiMatchStart, Match, excludedPlayers);

        ApplyNewChanges();
    }

    public void EndGame()
    {
        foreach (var slot in Slots.Values)
        {
            if (slot.UserId == -1)
                continue;

            if (slot.Status != MultiSlotStatus.Complete)
                return;

            slot.UpdateStatus(MultiSlotStatus.NotReady);
        }

        Match.InProgress = false;
        ResetGameStatuses();

        WriteToAllPlayers(PacketType.ServerMultiMatchFinished, 0);

        ApplyNewChanges();
    }

    public void LockSlot(int slotId)
    {
        var slot = GetSlot(id: slotId);

        if (slot == null || slot.UserId == Match.HostId || Match.InProgress)
            return;

        slot.UpdateLock();

        ApplyNewChanges();
    }

    public void ChangeTeam(Session session)
    {
        var slot = GetSlot(userId: session.User.Id);
        var matchInTeamMode = Match.MultiTeamType is MultiTeamTypes.TeamVs or MultiTeamTypes.TagTeamVs;

        if (slot == null || Match.InProgress || !matchInTeamMode)
            return;

        slot.UpdateTeam();

        ApplyNewChanges(false);
    }

    public void ChangeMods(Session session, Mods mods)
    {
        if (Match.SpecialModes == MultiSpecialModes.None)
        {
            if (session.User.Id != Match.HostId)
                return;

            Match.ActiveMods = mods;

            ApplyNewChanges();
            return;
        }

        if (session.User.Id == Match.HostId)
        {
            Match.ActiveMods = mods & (Mods.DoubleTime | Mods.Nightcore | Mods.HalfTime);
        }

        var slot = GetSlot(userId: session.User.Id);

        if (slot == null)
            return;

        slot.UpdateMods(mods);

        ApplyNewChanges();
    }

    public void MovePlayer(Session session, int slotId)
    {
        var slot = GetSlot(userId: session.User.Id);
        var newSlot = GetSlot(id: slotId);

        if (slot == null || newSlot == null || Match.InProgress)
            return;

        newSlot.AddPlayer(session.User.Id, slot.Status);

        slot.RemovePlayer();

        ApplyNewChanges();
    }

    public void UpdatePlayerStatus(Session session, MultiSlotStatus status)
    {
        var slot = GetSlot(userId: session.User.Id);

        if (slot == null)
            return;

        slot.UpdateStatus(status);

        ApplyNewChanges();
    }

    public void ChangePassword(Session session, string password)
    {
        if (session.User.Id != Match.HostId)
            return;

        Match.GamePassword = password.Replace(" ", "_");

        if (string.IsNullOrEmpty(Match.GamePassword))
            Match.GamePassword = null;

        ApplyNewChanges();
    }

    public void TransferHost(Session session, int slotId)
    {
        if (session.User.Id != Match.HostId)
            return;

        var newHostId = Match.SlotId[slotId];

        if (!Players.ContainsKey(newHostId))
            return;

        Match.HostId = newHostId;

        ApplyNewChanges();
    }

    public void SetPlayerLoaded(Session session)
    {
        var slot = GetSlot(userId: session.User.Id);

        if (slot == null)
            return;

        slot.UpdateIsLoaded(true);

        if (!Slots.Values.Any(s => s is { Status: MultiSlotStatus.Playing, IsLoaded: false }))
        {
            WriteToAllPlayers(PacketType.ServerMultiAllPlayersLoaded, Match);
        }
    }

    public void SetPlayerSkipped(Session session)
    {
        var slot = GetSlot(userId: session.User.Id);

        if (slot == null)
            return;

        slot.UpdateIsSkipped(true);

        var index = Array.IndexOf(Match.SlotId, session.User.Id);
        SendPlayerSkipped(index);

        if (!Slots.Values.Any(s => s is { Status: MultiSlotStatus.Playing, IsSkipped: false }))
        {
            WriteToAllPlayers(PacketType.ServerMultiSkip, 0);
        }
    }

    public void SetPlayerCompleted(Session session)
    {
        var slot = GetSlot(userId: session.User.Id);

        if (slot == null)
            return;

        slot.UpdateStatus(MultiSlotStatus.Complete);

        if (!Slots.Values.Any(s => s is { Status: MultiSlotStatus.Playing }))
        {
            EndGame();
        }
    }

    public void SendPlayerScoreUpdate(Session session, BanchoScoreFrame score)
    {
        var index = Array.IndexOf(Match.SlotId, session.User.Id);
        SendPlayerScoreUpdate(index, score);
    }

    public void SendPlayerFailed(Session session)
    {
        var index = Array.IndexOf(Match.SlotId, session.User.Id);
        SendPlayerFailed(index);
    }

    private void SendPlayerSkipped(int slotId)
    {
        WriteToAllPlayers(PacketType.ServerMultiSkipRequestOther, slotId);
    }

    private void SendPlayerFailed(int slotId)
    {
        WriteToAllPlayers(PacketType.ServerMultiOtherFailed, slotId);
    }

    private void SendPlayerScoreUpdate(int slotId, BanchoScoreFrame score)
    {
        score.Id = (byte)slotId;
        WriteToAllPlayers(PacketType.ServerMultiScoreUpdate, score);
    }

    private void ResetGameStatuses()
    {
        foreach (var slot in Slots.Values)
        {
            slot.UpdateIsLoaded(false);
            slot.UpdateIsSkipped(false);
        }
    }

    private MultiplayerSlot? GetSlot(int? id = null, int? userId = null)
    {
        if (id != null && userId != null)
        {
            throw new ArgumentException("Either id or userId must be provided, not both.");
        }

        if (id != null)
        {
            return Slots.GetValueOrDefault(id.Value);
        }

        if (userId != null)
        {
            return Slots.Values.FirstOrDefault(slot => slot.UserId == userId);
        }

        throw new ArgumentException("Either id or userId must be provided.");
    }

    private MultiplayerSlot? GetSlot(MultiSlotStatus status)
    {
        return Slots.Values.FirstOrDefault(slot => slot.Status == status);
    }

    private void UpdateSlots()
    {
        foreach (var (slot, index) in Slots.Values.Select((value, i) => (value, i)))
        {
            SetSlot(slot, index);
        }
    }

    private void SetSlot(MultiplayerSlot slot, int index = -1)
    {
        Match.SlotId[index] = slot.UserId;
        Match.MultiSlotStatus[index] = slot.Status;
        Match.SlotMods[index] = slot.Mods;
        Match.SlotTeam[index] = slot.Team;
    }

    private void WriteToAllPlayers(PacketType type, object data, int[]? exclude = null)
    {
        foreach (var player in Players.Values)
        {
            if (exclude != null && exclude.Contains(player.User.Id))
                continue;

            player.WritePacket(type, data);
        }
    }

    private void ApplyNewChanges(bool sendToLobby = true)
    {
        UpdateSlots();

        var removedPlayers = Players.Keys.Except(Match.SlotId).ToArray();
        var addedPlayers = Match.SlotId.Except(removedPlayers).Where(x => x != -1).ToArray();

        foreach (var addedPlayer in addedPlayers)
        {
            Players[addedPlayer].Match = this;
        }

        foreach (var removedPlayer in removedPlayers)
        {
            Players[removedPlayer].Match = null;
            Players.TryRemove(removedPlayer, out _);
        }

        if (Players.IsEmpty)
        {
            Matches.RemoveMatch(Match.MatchId);
        }
        else
        {
            WriteToAllPlayers(PacketType.ServerMultiMatchUpdate, Match);
            if (sendToLobby) Matches.WriteUpdateToLobby(this);
        }
    }
}