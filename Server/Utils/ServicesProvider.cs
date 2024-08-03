using Sunrise.Server.Data;
using Sunrise.Server.Repositories;

namespace Sunrise.Server.Utils;

public class ServicesProvider(SessionRepository sessions, SunriseDb database, RedisRepository redis)
{
    public SessionRepository Sessions { get; } = sessions;
    public SunriseDb Database { get; } = database;
    public RedisRepository Redis { get; } = redis;
}