using Sunrise.Shared.Database.Services;
using Sunrise.Shared.Database.Services.Events;

namespace Sunrise.Shared.Database.Repositories;

public class EventRepository
{
    private readonly SunriseDbContext _dbContext;
    private readonly DatabaseService _databaseService;

    public EventRepository(DatabaseService databaseService)
    {
        _databaseService = databaseService;
        _dbContext = databaseService.DbContext;

        Users = new UserEventService(_databaseService);
    }

    public UserEventService Users { get; }
}