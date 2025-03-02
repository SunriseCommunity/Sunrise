using HOPEless.Bancho;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Packets;

public class PacketHandler(IPacketHandler handler, bool suppressLogging)
{
    public bool SuppressLogging { get; } = suppressLogging;

    public Task Handle(BanchoPacket packet, Session session)
    {
        return handler.Handle(packet, session);
    }
}