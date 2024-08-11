using osu.Shared;

namespace Sunrise.Server.Objects.CustomAttributes;

[AttributeUsage(AttributeTargets.Class)]
public class ChatCommandAttribute(string command, PlayerRank requiredRank = PlayerRank.Default) : Attribute
{
    public string Command { get; } = command;
    public PlayerRank RequiredRank { get; set; } = requiredRank;
}