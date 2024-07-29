using Sunrise.GameClient.Repositories;
namespace Sunrise;

public class ServicesProvider
{
    public SessionRepository Sessions { get; }
    public Database.Database Database { get; }

    public ServicesProvider(SessionRepository sessions, Database.Database database)
    {
        Sessions = sessions;
        Database = database;
    }
}