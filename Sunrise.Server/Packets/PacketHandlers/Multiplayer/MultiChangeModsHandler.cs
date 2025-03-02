using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using osu.Shared;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Server.Packets.PacketHandlers.Multiplayer;

[PacketHandler(PacketType.ClientMultiChangeMods)]
public class MultiChangeModsHandler : IPacketHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        var mods = new BanchoInt(packet.Data);

        session.Match?.ChangeMods(session, (Mods)mods.Value);

        return Task.CompletedTask;
    }
}