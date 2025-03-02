using HOPEless.Bancho;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Packets;

public interface IPacketHandler
{
    Task Handle(BanchoPacket packet, Session session);
}