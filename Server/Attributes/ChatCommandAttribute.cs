using Sunrise.Server.Types.Enums;

namespace Sunrise.Server.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class ChatCommandAttribute(string command, string? prefix = "", UserPrivileges requiredPrivileges = UserPrivileges.User, bool isGlobal = false) : Attribute
{
    public string Command { get; } = command;
    public UserPrivileges RequiredPrivileges { get; set; } = requiredPrivileges;
    public bool IsGlobal { get; set; } = isGlobal;
    public string Prefix { get; set; } = prefix ?? string.Empty;
}