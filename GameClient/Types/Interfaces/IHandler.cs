using HOPEless.Bancho;
using Sunrise.Services;

namespace Sunrise.Types.Interfaces;

public interface IHandler
{
    void Handle(BanchoPacket packet, BanchoService banchoSession, ServicesProvider servicesProvider);
}