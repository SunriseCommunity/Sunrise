using HOPEless.Bancho;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Handlers.Spectate;

[PacketHandler(PacketType.ClientSpectateStop)]
public class SpectateStopHandler : IHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        var chatChannels = ServicesProviderHolder.ServiceProvider.GetRequiredService<ChannelRepository>();

        chatChannels.LeaveChannel($"#spectator_{session.Spectating?.User.Id}", session, true);

        session.Spectating?.RemoveSpectator(session);
        session.Spectating = null;

        return Task.CompletedTask;
    }
}