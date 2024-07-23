
using HOPEless.Bancho;
using Sunrise.Database.Sqlite;
using Sunrise.Services;

namespace Sunrise.Handlers;

public class StatusRequestOwnHandler : IHandler
{
    public void Handle(BanchoPacket packet, BanchoService banchoSession, SqliteDatabase database)
    {
        banchoSession.SendUserStats();
    }
}