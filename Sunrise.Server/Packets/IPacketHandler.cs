using HOPEless.Bancho;
using Sunrise.Shared.Objects.Session;

namespace Sunrise.Server.Packets;

public interface IPacketHandler
{
    Task Handle(BanchoPacket packet, Session session);
}