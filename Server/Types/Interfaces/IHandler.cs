using HOPEless.Bancho;
using Sunrise.Server.Objects;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Types.Interfaces;

public interface IHandler
{
    Task Handle(BanchoPacket packet, Session session, ServicesProvider services);
}