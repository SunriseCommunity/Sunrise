using HOPEless.Bancho;
using Sunrise.Database.Sqlite;
using Sunrise.Services;

namespace Sunrise.Handlers;

public interface IHandler
{
    void Handle(BanchoPacket packet, BanchoService banchoSession, SqliteDatabase database);
}