using System.Collections.Concurrent;
using HOPEless.Bancho;
using HOPEless.Bancho.Objects;
using HOPEless.Extensions;
using osu.Shared;
using osu.Shared.Serialization;
using Sunrise.Shared.Enums.Users;

namespace Sunrise.Shared.Helpers;

public class PacketHelper
{
    private readonly ConcurrentQueue<BanchoPacket> _packets = new();

    private void WritePacket(PacketType type, ISerializable data)
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
        {
            w.Write(i);
        }

        WritePacket(type, ((MemoryStream)w.BaseStream).ToArray());
    }

    private void WritePacket(PacketType type, List<string> data)
    {
        using var w = new SerializationWriter(new MemoryStream());
        w.Write((short)data.Count);

        foreach (var i in data)
        {
            w.Write(i);
        }

        WritePacket(type, ((MemoryStream)w.BaseStream).ToArray());
    }

    public void WritePacket<T>(PacketType type, T data)
    {
        switch (data)
        {
            // @formatter:off
            case int i: WritePacket(type, i); return;
            case string s: WritePacket(type, s); return;
            case List<int> il: WritePacket(type, il); return;
            case List<string> sl: WritePacket(type, sl); return;
            case PlayerRank pr: WritePacket(type, (int)pr); return;
            case LoginResponse lr: WritePacket(type, (int)lr); return;
            case ISerializable serializable: WritePacket(type, serializable); return;
            case byte[] bytes: WritePacket(type, bytes); return;
            default: throw new ArgumentException("Invalid data type: " + data?.GetType().Name);
            // @formatter:on
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