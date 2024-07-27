using HOPEless.Bancho;
using Sunrise.GameClient.Objects;
using Sunrise.GameClient.Types.Interfaces;

namespace Sunrise.GameClient.Handlers;
public class StatusRequestOwnHandler : IHandler
{
    public void Handle(BanchoPacket packet, Session session, ServicesProvider services)
    {
        session.SendUserData();
    }
}