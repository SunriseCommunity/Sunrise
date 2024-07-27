using System.Collections.Concurrent;
using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using HOPEless.Extensions;
using osu.Shared;
using osu.Shared.Serialization;
using Sunrise.GameClient.Types.Enums;

namespace Sunrise.GameClient.Objects;

public class PacketWriter
{
    private readonly ConcurrentQueue<BanchoPacket> _packets = new ConcurrentQueue<BanchoPacket>();

    [Obsolete("Use WritePacket instead.")]
    public void EnqueuePacket(BanchoPacket packet) => _packets.Enqueue(packet);

    private void WritePacket<T>(PacketType type, T data) where T : ISerializable
    {
        _packets.Enqueue(new BanchoPacket(type, data.Serialize()));
    }

    private void WritePacket(PacketType type, byte[] data)
    {
        _packets.Enqueue(new BanchoPacket(type, data));
    }

    private void WritePacket(PacketType type, int data)
    {
        WritePacket(type, new BanchoInt(data));
    }

    private void WritePacket(PacketType type, string data)
    {
        WritePacket(type, new BanchoString(data));
    }

    private void WritePacket(PacketType type, List<int> data)
    {
        using var w = new SerializationWriter(new MemoryStream());
        w.Write((short)data.Count);
        foreach (var i in data)
            w.Write(i);

        WritePacket(type, ((MemoryStream)w.BaseStream).ToArray());
    }

    private void WritePacket(PacketType type, List<string> data)
    {
        using var w = new SerializationWriter(new MemoryStream());
        w.Write((short)data.Count);
        foreach (var i in data)
            w.Write(i);

        WritePacket(type, ((MemoryStream)w.BaseStream).ToArray());
    }

    public void WritePacket(PacketType type, object data)
    {
        switch (data)
        {
            case int i: WritePacket(type, i); break;
            case string s: WritePacket(type, s); break;
            case List<int> il: WritePacket(type, il); break;
            case List<string> sl: WritePacket(type, sl); break;
            case PlayerRank pr: WritePacket(type, (int)pr); break;
            case LoginResponses lr: WritePacket(type, (int)lr); break;
            case ISerializable serializable: WritePacket(type, serializable); break;
            case byte[] bytes: WritePacket(type, bytes); break;
            default: throw new ArgumentException("Invalid data type: " + data.GetType().Name);
        }
    }

    public byte[] GetBytesToSend()
    {
        using var writer = new SerializationWriter(new MemoryStream());

        while (_packets.TryDequeue(out var packet))
        {
            packet.WriteToStream(writer);
        }

        return ((MemoryStream)writer.BaseStream).ToArray();
    }
}