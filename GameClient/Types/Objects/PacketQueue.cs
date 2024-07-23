using System.Collections.Concurrent;
using HOPEless.Bancho;
using osu.Shared.Serialization;

namespace Sunrise.GameClient.Types.Objects;

public class PacketQueue
{
    private ConcurrentQueue<BanchoPacket> _queue = new ConcurrentQueue<BanchoPacket>();

    public int Size => _queue.Count;

    public void EnqueuePacket(BanchoPacket packet) => _queue.Enqueue(packet);

    public byte[] GetBytesToSend()
    {
        using var writer = new SerializationWriter(new MemoryStream());

        while (_queue.TryDequeue(out var packet))
        {
            packet.WriteToStream(writer);
        }

        return ((MemoryStream)writer.BaseStream).ToArray();
    }
}