namespace Sunrise.Shared.Enums.Users;

[Flags]
public enum UserPlaystyle
{
    None = 0,
    Mouse = 1 << 0,
    Keyboard = 1 << 1,
    Tablet = 1 << 2,
    TouchScreen = 1 << 3
}