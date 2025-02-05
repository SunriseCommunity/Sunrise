using HOPEless.Bancho;

namespace Sunrise.Server.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class PacketHandlerAttribute(PacketType packetType, bool suppressLogger = false) : Attribute
{
    public PacketType PacketType { get; } = packetType;
    public bool SuppressLogger { get; } = suppressLogger;
}