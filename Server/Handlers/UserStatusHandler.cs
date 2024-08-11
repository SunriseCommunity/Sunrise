using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using HOPEless.osu;
using osu.Shared;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.CustomAttributes;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Handlers;

[PacketHandler(PacketType.ClientUserStatus)]
public class UserStatusHandler : IHandler
{
    public async Task Handle(BanchoPacket packet, Session session)
    {
        var status = new BanchoUserStatus(packet.Data);

        if (status.CurrentMods != Mods.None && status.Action is (BanchoAction.Playing or BanchoAction.Multiplaying))
        {
            status.ActionText += $" + {status.CurrentMods.ToString()}";
        }

        session.Attributes.Status = status;

        var sessions = ServicesProviderHolder.ServiceProvider.GetRequiredService<SessionRepository>();

        sessions.WriteToAllSessions(PacketType.ServerUserData, await session.Attributes.GetPlayerData());
    }
}