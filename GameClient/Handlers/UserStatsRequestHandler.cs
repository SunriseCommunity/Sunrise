using HOPEless.Bancho;
using Sunrise.Services;
using Sunrise.Types.Interfaces;

namespace Sunrise.Handlers;

public class UserStatsRequestHandler : IHandler
{

    public void Handle(BanchoPacket packet, BanchoService banchoSession, ServicesProvider services)
    {
        banchoSession.SendUserStats();
    }
}