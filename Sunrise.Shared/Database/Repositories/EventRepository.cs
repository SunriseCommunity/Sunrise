using Sunrise.Shared.Database.Services.Events;

namespace Sunrise.Shared.Database.Repositories;

public class EventRepository(UserEventService userEventService, BeatmapEventService beatmapEventService)
{
    public UserEventService Users { get; } = userEventService;
    public BeatmapEventService Beatmaps { get; } = beatmapEventService;
}