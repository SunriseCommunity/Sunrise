using HOPEless.Bancho;
using Sunrise.Database.Sqlite;
using Sunrise.Services;

namespace Sunrise.Handlers;

public class PongHandler : IHandler
{
    public void Handle(BanchoPacket packet, BanchoService banchoSession, SqliteDatabase database) { }
}