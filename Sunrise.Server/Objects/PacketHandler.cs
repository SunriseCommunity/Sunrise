using HOPEless.Bancho;
using Sunrise.Server.Types.Interfaces;
using ISession = Sunrise.Shared.Types.Interfaces.ISession;

namespace Sunrise.Server.Objects;

public class PacketHandler(IHandler handler, bool suppressLogging)
{
    public bool SuppressLogging { get; } = suppressLogging;

    public Task Handle(BanchoPacket packet, ISession session)
    {
        return handler.Handle(packet, session as Session);
    }
}