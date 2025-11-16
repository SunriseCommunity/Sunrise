using Sunrise.Shared.Enums.Users;

namespace Sunrise.Shared.Extensions.Users;

public static class UserPrivilegeExtensions
{
    public static UserPrivilege GetHighestPrivilege(this UserPrivilege p)
    {
        return Enum.GetValues<UserPrivilege>()
            .Where(v => p.HasFlag(v))
            .Max();
    }
}