using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using osu.Shared;
using Sunrise.Server.Attributes;
using Sunrise.Server.Objects;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Handlers.Multiplayer;

[PacketHandler(PacketType.ClientMultiChangeMods)]
public class MultiChangeModsHandler : IHandler
{
    public Task Handle(BanchoPacket packet, Session session)
    {
        var mods = new BanchoInt(packet.Data);

        session.Match?.ChangeMods(session, (Mods)mods.Value);

        return Task.CompletedTask;
    }
}