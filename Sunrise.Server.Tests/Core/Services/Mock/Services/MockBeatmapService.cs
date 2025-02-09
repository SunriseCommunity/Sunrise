using Sunrise.Server.Application;
using Sunrise.Server.Database;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Objects.Serializable;
using Sunrise.Server.Types.Enums;

namespace Sunrise.Server.Tests.Core.Services.Mock.Services;

public class MockBeatmapService(MockService service)
{
    public BeatmapStatus GetRandomBeatmapStatus()
    {
        var random = new Random();
        var values = Enum.GetValues(typeof(BeatmapStatus));
        return (BeatmapStatus)values.GetValue(random.Next(values.Length))!;
    }
    
    public BeatmapSet GetRandomBeatmapSet()
    {
        return new BeatmapSet
        {
            Id = service.GetRandomInteger(),
            Title = service.GetRandomString(),
            Artist = service.GetRandomString(),
            Creator = service.GetRandomString(),
            Offset = 0,
            LastUpdated = service.GetRandomDateTime(),
            SubmittedDate = service.GetRandomDateTime(),
            Beatmaps = [
                new Beatmap()
                {
                    Id = service.GetRandomInteger(),
                    Checksum = service.GetRandomString(),
                    BeatmapsetId = service.GetRandomInteger(),
                    LastUpdated = service.GetRandomDateTime(),
                }
            ]
        };
    }
    
    public async Task<BeatmapSet> MockRandomBeatmapSet()
    {
        var beatmapSet = service.Beatmap.GetRandomBeatmapSet();
        
        return await MockBeatmapSet(beatmapSet);
    }
    
    public async Task<BeatmapSet> MockBeatmapSet(BeatmapSet beatmapSet)
    {
        return await service.Redis.MockBeatmapSetCache(beatmapSet);
    }
}