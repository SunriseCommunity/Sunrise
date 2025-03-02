using System.Collections.Concurrent;
using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using HOPEless.osu;
using osu.Shared;
using Sunrise.Shared.Application;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Repositories.Multiplayer;

namespace Sunrise.Shared.Objects.Multiplayer;

public class MultiplayerMatch
{
    private readonly int _roomCreatorId;

    public MultiplayerMatch(MatchRepository matches, BanchoMultiplayerMatch newMatch)
    {
        Match = newMatch;
        Matches = matches;
        _roomCreatorId = newMatch.HostId;

        foreach (var (_, index) in Match.SlotId.Select((value, i) => (value, i)))
        {
            var isLocked = Match.MultiSlotStatus[index] == MultiSlotStatus.Locked;
            Slots.TryAdd(index, new MultiplayerSlot(isLocked));
        }
    }

    // In the locked room players can’t change their team and slot.
    public bool Locked { get; set; } = false;

    public BanchoMultiplayerMatch Match { get; private set; }
    private MatchRepository Matches { get; }
    public ConcurrentDictionary<int, Session> Players { get; } = new();
    public ConcurrentDictionary<int, MultiplayerSlot> Slots { get; } = new();
    private MultiplayerTimer? Timer { get; set; }

    public void UpdateMatchSettings(BanchoMultiplayerMatch updatedMatch, Session session)
    {
        if (Match.MatchId != updatedMatch.MatchId || !HasHostPrivileges(session))
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
                        Slots[index].UpdateMods(Match.ActiveMods & ~(Mods.DoubleTime | Mods.Nightcore | Mods.HalfTime));
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
                        Slots[index].UpdateMods(Mods.None);
                }
            }
        }

        if (updatedMatch.MultiTeamType != Match.MultiTeamType)
            foreach (var (slot, index) in updatedMatch.SlotId.Select((value, i) => (value, i)))
            {
                switch (updatedMatch.MultiTeamType)
                {
                    case MultiTeamTypes.TagTeamVs:
                    case MultiTeamTypes.TeamVs:
                        Slots[index].UpdateTeam(index % 2 == 0 ? SlotTeams.Blue : SlotTeams.Red);
                        break;
                    case MultiTeamTypes.HeadToHead:
                    case MultiTeamTypes.TagCoop:
                    default:
                        Slots[index].UpdateTeam(SlotTeams.Neutral);
                        break;
                }
            }

        Match = updatedMatch;
        ApplyNewChanges();
    }

    public void AddPlayer(Session session)
    {
        if (session.Match != null || Players.ContainsKey(session.UserId))
        {
            session.SendMultiMatchJoinFail();
            return;
        }

        var openSlot = GetSlot(MultiSlotStatus.Open);

        if (openSlot == null)
        {
            session.SendMultiMatchJoinFail();
            return;
        }

        Players.TryAdd(session.UserId, session);

        openSlot.AddPlayer(session.UserId);

        session.SendMultiMatchJoinSuccess(Match);

        var chatChannels = ServicesProviderHolder.GetRequiredService<ChatChannelRepository>();
        chatChannels.JoinChannel($"#multiplayer_{Match.MatchId}", session, true);

        ApplyNewChanges();
    }

    public void RemovePlayer(Session session, bool forced = false)
    {
        var slot = GetSlot(userId: session.UserId);

        if (slot == null)
            return;

        if (slot.UserId == Match.HostId)
        {
            var newHost = Players.Values.FirstOrDefault(player => player.UserId != Match.HostId);
            Match.HostId = newHost?.UserId ?? -1;
        }

        slot.RemovePlayer();

        var chatChannels = ServicesProviderHolder.GetRequiredService<ChatChannelRepository>();
        chatChannels.LeaveChannel($"#multiplayer_{Match.MatchId}", session, true);

        if (forced)
        {
            session.SendNotification("You have been kicked from the multiplayer room.");
            session.SendMultiMatchJoinFail();
        }

        ApplyNewChanges();
    }

    public void StartGame()
    {
        // Soft fix for InProgress being falsely true
        if (Match.InProgress && Slots.Values.Any(s => s is { Status: MultiSlotStatus.Playing }))
            return;

        foreach (var slot in Slots.Values)
        {
            if (slot.UserId == -1)
                continue;

            if (slot.Status == MultiSlotStatus.NoMap)
                continue;

            slot.UpdateStatus(MultiSlotStatus.Playing);
        }

        Match.InProgress = true;
        ResetGameStatuses();

        var excludedPlayers = Slots.Values.Where(s => s.UserId == -1 || s.Status == MultiSlotStatus.NoMap)
            .Select(s => s.UserId).ToArray();
        WriteToAllPlayers(PacketType.ServerMultiMatchStart, Match, excludedPlayers);

        ApplyNewChanges();
    }

    public bool HasHostPrivileges(Session session, bool shouldBeOwner = false)
    {
        return !shouldBeOwner && session.UserId == Match.HostId || session.UserId == _roomCreatorId;
    }

    public void EndGame(bool forced = false)
    {
        foreach (var slot in Slots.Values)
        {
            if (slot.UserId == -1 || slot.Status == MultiSlotStatus.NoMap)
                continue;

            slot.UpdateStatus(MultiSlotStatus.NotReady);
        }

        Match.InProgress = false;
        ResetGameStatuses();

        WriteToAllPlayers(forced ? PacketType.ClientMultiAbort : PacketType.ServerMultiMatchFinished, 0);

        if (forced)
            WriteToAllPlayers(PacketType.ServerNotification, "The match has been forcefully ended by the host.");

        ApplyNewChanges();
    }

    public void StartTimer(int timer, bool timerForStart, Func<MultiplayerMatch, string, Task> alertHandler,
        Func<MultiplayerMatch, Task> finishHandler)
    {
        StopTimer();

        var alertMessage = timerForStart ? "The match will start in {0}." : "Countdown will end in {0}.";
        Timer = new MultiplayerTimer(timer, alertMessage, this, finishHandler, alertHandler);
    }

    public void StopTimer()
    {
        Timer?.Stop();
        Timer = null;
    }

    public bool HasActiveTimer()
    {
        return Timer != null;
    }

    public void UpdateLock(int slotId, bool? toLock = null)
    {
        var slot = GetSlot(slotId);

        if (slot == null || slot.UserId == Match.HostId || Match.InProgress)
            return;

        slot.UpdateLock(toLock);

        ApplyNewChanges();
    }

    public void ChangeTeam(Session session, SlotTeams? team = null, bool ignoreLock = false)
    {
        if (Locked && !ignoreLock)
            return;

        var slot = GetSlot(userId: session.UserId);
        var matchInTeamMode = Match.MultiTeamType is MultiTeamTypes.TeamVs or MultiTeamTypes.TagTeamVs;

        if (slot == null || Match.InProgress || !matchInTeamMode)
            return;

        slot.UpdateTeam(team);

        ApplyNewChanges(false);
    }

    public void ChangeMods(Session session, Mods mods)
    {
        if (Match.SpecialModes == MultiSpecialModes.None)
        {
            if (session.UserId != Match.HostId)
                return;

            Match.ActiveMods = mods;

            ApplyNewChanges();
            return;
        }

        if (session.UserId == Match.HostId)
            Match.ActiveMods = mods & (Mods.DoubleTime | Mods.Nightcore | Mods.HalfTime);

        var slot = GetSlot(userId: session.UserId);

        if (slot == null)
            return;

        slot.UpdateMods(mods);

        ApplyNewChanges();
    }

    public void MovePlayer(Session session, int slotId, bool ignoreLock = false)
    {
        if (Locked && !ignoreLock)
            return;

        var slot = GetSlot(userId: session.UserId);
        var newSlot = GetSlot(slotId);

        if (slot == null || newSlot is not { UserId: -1 } || Match.InProgress)
            return;

        newSlot.AddPlayer(slot);

        slot.RemovePlayer();

        ApplyNewChanges();
    }

    public void UpdatePlayerStatus(Session session, MultiSlotStatus status)
    {
        var slot = GetSlot(userId: session.UserId);

        if (slot == null)
            return;

        slot.UpdateStatus(status);

        ApplyNewChanges();
    }

    public void ChangePassword(string? password = null)
    {
        Match.GamePassword = password?.Replace(" ", "_");

        if (string.IsNullOrEmpty(Match.GamePassword))
            Match.GamePassword = null;

        WriteToAllPlayers(PacketType.ServerMultiChangePassword, Match.GamePassword ?? "");

        ApplyNewChanges();
    }

    public void TransferHost(int slotId)
    {
        var newHostId = Match.SlotId[slotId];

        if (!Players.ContainsKey(newHostId))
            return;

        Match.HostId = newHostId;

        ApplyNewChanges();
    }

    public void ClearHost()
    {
        Match.HostId = -1;

        ApplyNewChanges();
    }


    public void SetPlayerLoaded(Session session)
    {
        var slot = GetSlot(userId: session.UserId);

        if (slot == null)
            return;

        slot.UpdateIsLoaded(true);

        if (!Slots.Values.Any(s => s is { Status: MultiSlotStatus.Playing, IsLoaded: false }))
            WriteToAllPlayers(PacketType.ServerMultiAllPlayersLoaded, Match);
    }

    public void SetPlayerSkipped(Session session)
    {
        var slot = GetSlot(userId: session.UserId);

        if (slot == null)
            return;

        slot.UpdateIsSkipped(true);

        var index = Array.IndexOf(Match.SlotId, session.UserId);
        SendPlayerSkipped(index);

        if (!Slots.Values.Any(s => s is { Status: MultiSlotStatus.Playing, IsSkipped: false }))
            WriteToAllPlayers(PacketType.ServerMultiSkip, 0);
    }

    public void SetPlayerCompleted(Session session)
    {
        var slot = GetSlot(userId: session.UserId);

        if (slot == null)
            return;

        slot.UpdateStatus(MultiSlotStatus.Complete);

        if (!Slots.Values.Any(s => s is { Status: MultiSlotStatus.Playing })) EndGame();
    }

    public void SendPlayerScoreUpdate(Session session, BanchoScoreFrame score)
    {
        var index = Array.IndexOf(Match.SlotId, session.UserId);
        SendPlayerScoreUpdate(index, score);
    }

    public void SendPlayerFailed(Session session)
    {
        var index = Array.IndexOf(Match.SlotId, session.UserId);
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
            throw new ArgumentException("Either id or userId must be provided, not both.");

        if (id != null) return Slots.GetValueOrDefault(id.Value);

        if (userId != null) return Slots.Values.FirstOrDefault(slot => slot.UserId == userId);

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
            if (exclude != null && exclude.Contains(player.UserId))
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