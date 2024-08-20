using HOPEless.Bancho;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Handlers.Spectate;

[PacketHandler(PacketType.ClientSpectateData)]
public class SpectateDataHandler : IHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        foreach (var spectator in session.Spectators)
        {
            spectator.WritePacket(PacketType.ServerSpectateData, packet.Data);
        }

        return Task.CompletedTask;
    }
}