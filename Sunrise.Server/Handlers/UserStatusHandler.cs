using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using HOPEless.osu;
using osu.Shared;
using Sunrise.Server.Application;
using Sunrise.Server.Attributes;
using Sunrise.Server.Objects;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types.Interfaces;

namespace Sunrise.Server.Handlers;

[PacketHandler(PacketType.ClientUserStatus)]
public class UserStatusHandler : IHandler
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