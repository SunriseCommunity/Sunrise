using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using osu.Shared.Serialization;
using Sunrise.Database.Sqlite;
using Sunrise.Services;

namespace Sunrise.Handlers;

public class DisconnectHandler : IHandler
{
    public void Handle(BanchoPacket packet, BanchoService banchoSession, SqliteDatabase database)
    {
        var writer = new SerializationWriter(new MemoryStream());

        // NOTE: Could it? Should it? Would it? Be possible to send a BanchoUserQuit packet to the player who is disconnecting? Idk.
        if (banchoSession.Player != null) writer.Write(new BanchoUserQuit(banchoSession.Player.Id));

        var p = new BanchoPacket(PacketType.ServerUserQuit, ((MemoryStream)writer.BaseStream).ToArray());

        banchoSession.EnqueuePacketForEveryone(packet);
    }
}