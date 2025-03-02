using System.Text;

namespace Sunrise.Shared.Utils;

internal class ReplayReader(Stream stream0) : BinaryReader(stream0, Encoding.UTF8)
{
    public override string ReadString()
    {
        if (ReadByte() == 0)
            return null;

        return base.ReadString();
    }

    public byte[] ReadByteArray()
    {
        var count = ReadInt32();
        if (count > 0)
            return ReadBytes(count);
        if (count < 0)
            return null;

        return new byte[0];
    }

    public DateTime ReadDateTime()
    {
        var ticks = ReadInt64();
        if (ticks < 0L)
            throw new AbandonedMutexException("oops");

        return new DateTime(ticks, DateTimeKind.Utc);
    }
}