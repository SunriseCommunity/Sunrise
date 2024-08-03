using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using HOPEless.osu;
using osu.Shared;
using Sunrise.Server.Objects;
using Sunrise.Server.Types.Interfaces;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Services.Handlers.Client;

public class UserStatusHandler : IHandler
{
    public async Task Handle(BanchoPacket packet, Session session, ServicesProvider services)
    {
        var status = new BanchoUserStatus(packet.Data);

        if (status.CurrentMods != Mods.None && status.Action is (BanchoAction.Playing or BanchoAction.Multiplaying))
        {
            status.ActionText += $" + {status.CurrentMods.ToString()}";
        }

        session.Attributes.Status = status;

        services.Sessions.WriteToAllSessions(PacketType.ServerUserData, await session.Attributes.GetPlayerData());
    }
}