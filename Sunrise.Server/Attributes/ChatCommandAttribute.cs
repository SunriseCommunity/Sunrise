using Sunrise.Shared.Enums.Users;

namespace Sunrise.Server.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class ChatCommandAttribute(string command, string? prefix = "", UserPrivilege requiredPrivileges = UserPrivilege.User, bool isGlobal = false) : Attribute
{
    public string Command { get; } = command;
    public UserPrivilege RequiredPrivileges { get; set; } = requiredPrivileges;
    public bool IsGlobal { get; set; } = isGlobal;
    public string Prefix { get; set; } = prefix ?? string.Empty;
}