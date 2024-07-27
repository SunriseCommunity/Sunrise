using HOPEless.Bancho;
using Sunrise.GameClient.Objects;
using Sunrise.GameClient.Types.Interfaces;

namespace Sunrise.GameClient.Handlers;

public class DisconnectHandler : IHandler
{
    public void Handle(BanchoPacket packet, Session session, ServicesProvider services)
    {
        session.SendUserQuit();

        services.Sessions.WriteToAllSessions(PacketType.ServerUserQuit, session.User.Id);
        services.Sessions.RemoveSession(session.User.Id);
    }
}