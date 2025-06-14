using HOPEless.Bancho;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Packets.PacketHandlers.Spectate;

[PacketHandler(PacketType.ClientSpectateData)]
public class SpectateDataHandler : IPacketHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        foreach (var (_, spectator) in session.Spectators)
        {
            spectator.WritePacket(PacketType.ServerSpectateData, packet.Data);
        }

        return Task.CompletedTask;
    }
}