using HOPEless.osu;
using osu.Shared;

namespace Sunrise.Server.Objects;

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

    public void RemovePlayer()
    {
        UserId = -1;
        Status = MultiSlotStatus.Open;
        Mods = Mods.None;
        Team = SlotTeams.Neutral;
        IsLoaded = false;
        IsSkipped = false;
    }

    public void UpdateLock()
    {
        Status = Status == MultiSlotStatus.Locked ? MultiSlotStatus.Open : MultiSlotStatus.Locked;
        UserId = -1;
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

    public void UpdateTeam()
    {
        Team = Team == SlotTeams.Blue ? SlotTeams.Red : SlotTeams.Blue;
    }
}