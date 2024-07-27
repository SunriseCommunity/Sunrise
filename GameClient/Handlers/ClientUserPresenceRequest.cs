using HOPEless.Bancho;
using Sunrise.GameClient.Objects;
using Sunrise.GameClient.Types.Interfaces;

namespace Sunrise.GameClient.Handlers;

public class ClientUserPresenceRequest : IHandler
{
    public void Handle(BanchoPacket packet, Session session, ServicesProvider services)
    {
        session.SendUserPresence();
    }
}

