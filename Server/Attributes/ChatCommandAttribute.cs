using osu.Shared;

namespace Sunrise.Server.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class ChatCommandAttribute(string command, string? prefix = "", PlayerRank requiredRank = PlayerRank.Default, bool isGlobal = false) : Attribute
{
    public string Command { get; } = command;
    public PlayerRank RequiredRank { get; set; } = requiredRank;
    public bool IsGlobal { get; set; } = isGlobal;
    public string Prefix { get; set; } = prefix ?? string.Empty;
}