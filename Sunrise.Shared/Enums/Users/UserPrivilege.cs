namespace Sunrise.Shared.Enums.Users;

[Flags]
public enum UserPrivilege
{
    User = 0,
    Supporter = 1 << 0,
    Admin = 1 << 3,
    Developer = 1 << 4,
    SuperUser = 1 << 9,
    ServerBot = 1 << 10,
    BeatmapApprovalTeamStandard = 1 << 11,
    BeatmapApprovalTeamTaiko = 1 << 12,
    BeatmapApprovalTeamCatch = 1 << 13,
    BeatmapApprovalTeamMania = 1 << 14
}
