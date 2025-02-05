using HOPEless.Bancho;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Objects;

public class PacketHandler(IHandler handler, bool suppressLogging)
{
    public bool SuppressLogging { get; } = suppressLogging;

    public Task Handle(BanchoPacket packet, Session session)
    {
        return handler.Handle(packet, session);
    }
}