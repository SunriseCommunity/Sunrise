using HOPEless.osu;
using osu.Shared;

namespace Sunrise.Shared.Types.Interfaces;

public interface IMultiplayerSlot
{
    // Properties
    int UserId { get; }
    MultiSlotStatus Status { get; }
    Mods Mods { get; }
    SlotTeams Team { get; }
    bool IsLoaded { get; }
    bool IsSkipped { get; }

    // Methods for managing slot state and players
    void AddPlayer(int userId, MultiSlotStatus? status = null);
    void AddPlayer(IMultiplayerSlot slot);
    void RemovePlayer();
    void UpdateLock(bool? toLock = null);
    void UpdateStatus(MultiSlotStatus status);
    void UpdateMods(Mods mods);
    void UpdateIsLoaded(bool? isLoaded);
    void UpdateIsSkipped(bool? isSkipped);
    void UpdateTeam(SlotTeams? team = null);
}