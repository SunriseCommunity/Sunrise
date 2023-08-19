using System.Reflection.Metadata;
using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using osu.Shared.Serialization;
using Sunrise.Objects;
using Sunrise.Services;

namespace Sunrise.Handlers;

public class DisconnectHandler : IHandler
{
    public void Handle(BanchoPacket packet, Player player, BanchoService bancho, PlayerRepository repository)
    {
        var writer = new SerializationWriter(new MemoryStream());

        writer.Write(new BanchoUserQuit(player.Id));

        var p = new BanchoPacket(PacketType.ServerUserQuit, ((MemoryStream)writer.BaseStream).ToArray());
        
        bancho.EnqueuePacketForEveryone(packet);
        repository.RemovePlayer(player.Id);
    }
}