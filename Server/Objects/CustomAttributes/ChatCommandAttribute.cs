using osu.Shared;

namespace Sunrise.Server.Objects.CustomAttributes;

[AttributeUsage(AttributeTargets.Class)]
public class ChatCommandAttribute(string command, PlayerRank requiredRank = PlayerRank.Default, bool isGlobal = false) : Attribute
{
    public string Command { get; } = command;
    public PlayerRank RequiredRank { get; set; } = requiredRank;
    public bool IsGlobal { get; set; } = isGlobal;
}