namespace Sunrise.Server.Types.Enums;

[Flags]
public enum UserPrivileges
{
    User = 0,
    Supporter = 1 << 0,
    Bat = 1 << 1,
    Admin = 1 << 3,
    Developer = 1 << 4
}