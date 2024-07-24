using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using HOPEless.osu;
using osu.Shared;
using Sunrise.Services;
using Sunrise.Types.Interfaces;

namespace Sunrise.Handlers;

public class UserStatusHandler : IHandler
{
    public void Handle(BanchoPacket packet, BanchoService banchoSession, ServicesProvider services)
    {
        var status = new BanchoUserStatus(packet.Data);

        if (status.CurrentMods != Mods.None && status.Action is (BanchoAction.Playing or BanchoAction.Multiplaying))
            status.ActionText += $" + {status.CurrentMods.ToString()}";
    }
}