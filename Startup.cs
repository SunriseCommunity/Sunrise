using Sunrise.Services;

namespace Sunrise;

public class ServicesProvider
{
    public PlayersPoolService Players { get; }
    public Database.Database Database { get; }

    public ServicesProvider(PlayersPoolService playersPoolService, Database.Database database)
    {
        Players = playersPoolService;
        Database = database;
    }
}