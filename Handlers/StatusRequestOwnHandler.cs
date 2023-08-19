
using HOPEless.Bancho;
using Sunrise.Objects;
using Sunrise.Services;

namespace Sunrise.Handlers;

public class StatusRequestOwnHandler : IHandler
{
    public void Handle(BanchoPacket packet, Player player,
        BanchoService bancho, PlayerRepository repository)
    {
        bancho.SendUserStats();
    }
}