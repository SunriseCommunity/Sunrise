using HOPEless.Bancho;
using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Database;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Handlers;

[PacketHandler(PacketType.ClientDisconnect)]
public class DisconnectHandler : IHandler
{
    public async Task Handle(BanchoPacket packet, Session session)
    {
        // Thanks osu! client for trying to disconnect us right after we connect :)
        if (session.Attributes.LastPingRequest > DateTime.UtcNow.AddSeconds(-2)) return;

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

        sessions.WriteToAllSessions(PacketType.ServerUserQuit, session.User.Id);
        sessions.RemoveSession(session);

        // Update last online time
        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();
        await session.UpdateUser(session.User);
        session.User.LastOnlineTime = DateTime.UtcNow;
        await database.UpdateUser(session.User);
    }
}