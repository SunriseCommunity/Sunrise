using HOPEless.Bancho;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Application;
using Sunrise.Shared.Objects.Session;
using Sunrise.Shared.Repositories;

namespace Sunrise.Server.Packets.PacketHandlers;

[PacketHandler(PacketType.ClientDisconnect)]
public class DisconnectHandler : IPacketHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();
        sessions.WriteToAllSessions(PacketType.ServerUserQuit, session.User.Id);
        sessions.RemoveSession(session);

        return Task.CompletedTask;
    }
}