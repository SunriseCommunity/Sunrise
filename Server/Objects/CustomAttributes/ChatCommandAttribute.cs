namespace Sunrise.Server.Objects.CustomAttributes;

[AttributeUsage(AttributeTargets.Class)]
public class ChatCommandAttribute(string command) : Attribute
{
    public string Command { get; } = command;
}