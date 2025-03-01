namespace Sunrise.Shared.Enums.Users;

[Flags]
public enum UserPrivilege
{
    User = 0,
    Supporter = 1 << 0,
    Bat = 1 << 1,
    Admin = 1 << 3,
    Developer = 1 << 4,
    ServerBot = 1 << 5,
}