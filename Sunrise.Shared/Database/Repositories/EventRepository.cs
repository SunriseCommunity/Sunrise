using Sunrise.Shared.Database.Services.Events;

namespace Sunrise.Shared.Database.Repositories;

public class EventRepository(UserEventService userEventService)
{
    public UserEventService Users { get; } = userEventService;
}