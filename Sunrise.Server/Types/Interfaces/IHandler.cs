using HOPEless.Bancho;
using Sunrise.Server.Objects;

namespace Sunrise.Server.Types.Interfaces;

public interface IHandler
{
    Task Handle(BanchoPacket packet, Session session);
}