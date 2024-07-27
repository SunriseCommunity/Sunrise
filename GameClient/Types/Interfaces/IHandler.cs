using HOPEless.Bancho;
using Sunrise.GameClient.Objects;

namespace Sunrise.GameClient.Types.Interfaces;

public interface IHandler
{
    void Handle(BanchoPacket packet, Session session, ServicesProvider services);
}