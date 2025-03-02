using HOPEless.osu;
using osu.Shared;

namespace Sunrise.Shared.Objects.Multiplayer;

public class MultiplayerSlot(bool isLocked = false)
{
    public int UserId { get; private set; } = -1;
    public MultiSlotStatus Status { get; private set; } = isLocked ? MultiSlotStatus.Locked : MultiSlotStatus.Open;
    public Mods Mods { get; private set; } = Mods.None;
    public SlotTeams Team { get; private set; } = SlotTeams.Neutral;
    public bool IsLoaded { get; private set; }
    public bool IsSkipped { get; private set; }

    public void AddPlayer(int userId, MultiSlotStatus? status = null)
    {
        UserId = userId;
        Status = status ?? MultiSlotStatus.NotReady;
    }

    public void AddPlayer(MultiplayerSlot slot)
    {
        UserId = slot.UserId;
        Status = slot.Status;
        Mods = slot.Mods;
        Team = slot.Team;
        IsLoaded = slot.IsLoaded;
        IsSkipped = slot.IsSkipped;
    }

    public void RemovePlayer()
    {
        UserId = -1;
        Status = MultiSlotStatus.Open;
        Mods = Mods.None;
        Team = SlotTeams.Neutral;
        IsLoaded = false;
        IsSkipped = false;
    }

    public void UpdateLock(bool? toLock = null)
    {
        Status = toLock ?? Status == MultiSlotStatus.Locked ? MultiSlotStatus.Open : MultiSlotStatus.Locked;
        UserId = Status == MultiSlotStatus.Locked ? UserId : -1;
    }

    public void UpdateStatus(MultiSlotStatus status)
    {
        Status = status;
    }

    public void UpdateMods(Mods mods)
    {
        Mods = mods;
    }

    public void UpdateIsLoaded(bool? isLoaded)
    {
        IsLoaded = isLoaded ?? !IsLoaded;
    }

    public void UpdateIsSkipped(bool? isSkipped)
    {
        IsSkipped = isSkipped ?? !IsSkipped;
    }

    public void UpdateTeam(SlotTeams? team = null)
    {
        Team = team ?? (Team == SlotTeams.Blue ? SlotTeams.Red : SlotTeams.Blue);
    }
}