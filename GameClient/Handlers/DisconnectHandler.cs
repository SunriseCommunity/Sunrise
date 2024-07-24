using HOPEless.Bancho;
using Sunrise.Services;
using Sunrise.Types.Interfaces;

namespace Sunrise.Handlers;

public class DisconnectHandler : IHandler
{


    public void Handle(BanchoPacket packet, BanchoService banchoSession, ServicesProvider services)
    {
        if (banchoSession.PlayerObject != null)
        {
            Console.WriteLine($"Player {banchoSession.PlayerObject.Player.Id} disconnected.");

            services.Players.RemovePlayer(banchoSession.PlayerObject.Player.Id);
        }

        banchoSession.EnqueuePacketForEveryone(packet);
    }
}