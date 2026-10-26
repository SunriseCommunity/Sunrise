using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Users;

namespace Sunrise.Shared.Extensions.Users;

public static class UserPrivilegeExtensions
{
    private const UserPrivilege BeatmapApprovalTeamPrivileges =
        UserPrivilege.BeatmapApprovalTeamStandard |
        UserPrivilege.BeatmapApprovalTeamTaiko |
        UserPrivilege.BeatmapApprovalTeamCatch |
        UserPrivilege.BeatmapApprovalTeamMania;

    public static bool HasAnyBeatmapApprovalTeamPrivilege(this UserPrivilege privilege) =>
        (privilege & BeatmapApprovalTeamPrivileges) != UserPrivilege.User;

    public static bool HasRequiredPrivilege(this UserPrivilege privilege, UserPrivilege requiredPrivilege)
    {
        if (requiredPrivilege == BeatmapApprovalTeamPrivileges)
            return privilege.HasAnyBeatmapApprovalTeamPrivilege();

        return privilege.HasFlag(requiredPrivilege);
    }

    public static UserPrivilege GetBeatmapApprovalTeamPrivilege(GameMode gameMode) => gameMode switch
    {
        GameMode.Standard => UserPrivilege.BeatmapApprovalTeamStandard,
        GameMode.Taiko => UserPrivilege.BeatmapApprovalTeamTaiko,
        GameMode.CatchTheBeat => UserPrivilege.BeatmapApprovalTeamCatch,
        GameMode.Mania => UserPrivilege.BeatmapApprovalTeamMania,
        _ => UserPrivilege.User
    };

    public static int GetPrivilegeLevel(this UserPrivilege p)
    {
        if (p.HasFlag(UserPrivilege.ServerBot)) return 6;
        if (p.HasFlag(UserPrivilege.SuperUser)) return 5;
        if (p.HasFlag(UserPrivilege.Developer)) return 4;
        if (p.HasFlag(UserPrivilege.Admin)) return 3;
        if (p.HasAnyBeatmapApprovalTeamPrivilege()) return 2;
        if (p.HasFlag(UserPrivilege.Supporter)) return 1;

        return 0;
    }
}
