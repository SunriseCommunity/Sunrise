using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using HOPEless.osu;
using osu.Shared;
using Sunrise.GameClient.Objects;
using Sunrise.GameClient.Types.Interfaces;

namespace Sunrise.GameClient.Handlers;

public class UserStatusHandler : IHandler
{
    public void Handle(BanchoPacket packet, Session session, ServicesProvider services)
    {
        var status = new BanchoUserStatus(packet.Data);

        if (status.CurrentMods != Mods.None && status.Action is (BanchoAction.Playing or BanchoAction.Multiplaying))
            status.ActionText += $" + {status.CurrentMods.ToString()}";

        session.Attributes.Status = status;
    }
}