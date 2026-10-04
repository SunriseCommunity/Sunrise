using Sunrise.Shared.Database.Services.Events;

namespace Sunrise.Shared.Database.Repositories;

public class EventRepository(UserEventService userEventService, BeatmapEventService beatmapEventService, ScoreProcessingEventService scoreProcessingEventService)
{
    public UserEventService Users { get; } = userEventService;
    public BeatmapEventService Beatmaps { get; } = beatmapEventService;
    public ScoreProcessingEventService ScoreProcessing { get; } = scoreProcessingEventService;
}