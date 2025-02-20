using HOPEless.Bancho;
using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Shared.Application;

namespace Sunrise.Server.Handlers.Spectate;

[PacketHandler(PacketType.ClientSpectateStop)]
public class SpectateStopHandler : IHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        var chatChannels = ServicesProviderHolder.GetRequiredService<ChannelRepository>();

        chatChannels.LeaveChannel($"#spectator_{session.Spectating?.User.Id}", session, true);

        session.Spectating?.RemoveSpectator(session);
        session.Spectating = null;

        return Task.CompletedTask;
    }
}