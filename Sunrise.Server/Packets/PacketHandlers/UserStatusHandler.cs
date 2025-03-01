using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using HOPEless.osu;
using osu.Shared;
using Sunrise.Server.Attributes;
using Sunrise.Shared.Application;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories;

namespace Sunrise.Server.Packets.PacketHandlers;

[PacketHandler(PacketType.ClientUserStatus)]
public class UserStatusHandler : IPacketHandler
{
    public async Task Handle(BanchoPacket packet, Session session)
    {
        var status = new BanchoUserStatus(packet.Data);

        if (status.CurrentMods != Mods.None && status.Action is (BanchoAction.Playing or BanchoAction.Multiplaying))
            status.ActionText += $" + {status.CurrentMods.ToString()}";

        session.Attributes.Status = status;

        var sessions = ServicesProviderHolder.GetRequiredService<SessionRepository>();

        sessions.WriteToAllSessions(PacketType.ServerUserData, await session.Attributes.GetPlayerData());
    }
}