using HOPEless.Bancho.Objects;

namespace Sunrise.Shared.Extensions.Users;

public static class BanchoUserStatusExtesnions
{
    public static string ToText(this BanchoUserStatus status)
    {
        return $"{status.Action} {status.ActionText}";
    }
}