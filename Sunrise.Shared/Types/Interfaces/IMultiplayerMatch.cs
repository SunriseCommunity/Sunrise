using System.Collections.Concurrent;
using HOPEless.Bancho.Objects;
using HOPEless.osu;
using osu.Shared;

namespace Sunrise.Shared.Types.Interfaces;

public interface IMultiplayerMatch
{
    // Properties
    bool Locked { get; set; }
    BanchoMultiplayerMatch Match { get; }
    ConcurrentDictionary<int, ISession> Players { get; }
    ConcurrentDictionary<int, IMultiplayerSlot> Slots { get; }

    // Methods for managing the match
    void UpdateMatchSettings(BanchoMultiplayerMatch updatedMatch, ISession session);

    void AddPlayer(ISession session);

    void RemovePlayer(ISession session, bool forced = false);

    void StartGame();

    void EndGame(bool forced = false);

    void StartTimer(int timer, bool timerForStart, Func<IMultiplayerMatch, string, Task> alertHandler, Func<IMultiplayerMatch, Task> finishHandler);

    void StopTimer();

    bool HasActiveTimer();

    void UpdateLock(int slotId, bool? toLock = null);

    void ChangeTeam(ISession session, SlotTeams? team = null, bool ignoreLock = false);

    void ChangeMods(ISession session, Mods mods);

    void MovePlayer(ISession session, int slotId, bool ignoreLock = false);

    void UpdatePlayerStatus(ISession session, MultiSlotStatus status);

    void ChangePassword(string? password = null);

    void TransferHost(int slotId);

    void ClearHost();

    void SetPlayerLoaded(ISession session);

    void SetPlayerSkipped(ISession session);

    void SetPlayerCompleted(ISession session);

    void SendPlayerScoreUpdate(ISession session, BanchoScoreFrame score);

    void SendPlayerFailed(ISession session);

    bool HasHostPrivileges(ISession session, bool shouldBeOwner = false);
}