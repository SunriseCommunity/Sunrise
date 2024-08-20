using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Repositories;
using Sunrise.Server.Repositories.Chat;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Handlers.Spectate;

[PacketHandler(PacketType.ClientSpectateStart)]
public class SpectateStartHandler : IHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        var target = new BanchoInt(packet.Data);

        var sessions = ServicesProviderHolder.ServiceProvider.GetRequiredService<SessionRepository>();
        var targetSession = sessions.GetSession(target.Value);

        targetSession?.AddSpectator(session);
        session.Spectating = targetSession;

        var chatChannels = ServicesProviderHolder.ServiceProvider.GetRequiredService<ChannelRepository>();
        chatChannels.JoinChannel($"#spectator_{targetSession?.User.Username}", session, true);

        return Task.CompletedTask;
    }
}