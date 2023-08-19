using HOPEless.Bancho;
using Sunrise.Objects;
using Sunrise.Services;

namespace Sunrise.Handlers;

public interface IHandler
{
    void Handle(BanchoPacket packet, Player player, BanchoService bancho, PlayerRepository repository);
}