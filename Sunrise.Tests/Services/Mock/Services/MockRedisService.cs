using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Objects.Serializable;

namespace Sunrise.Tests.Services.Mock.Services;

public class MockRedisService(MockService service)
{

    private readonly FileService _fileService = new();
    private static Dictionary<int, string>? BeatmapFileNamesById => Configuration.GetConfig().GetSection("Test:BeatmapFileNamesById").Get<Dictionary<int, string>>();
    private static Dictionary<string, string>? BeatmapFileNamesByHash => Configuration.GetConfig().GetSection("Test:BeatmapFileNamesByHash").Get<Dictionary<string, string>>();
    private static Dictionary<string, int>? BeatmapIdsByHashes => Configuration.GetConfig().GetSection("Test:BeatmapIdByHash").Get<Dictionary<string, int>>();

    public async Task<BeatmapSet> MockBeatmapSetCache()
    {
        ThrowIfCacheDisabled();

        var beatmapSet = service.Beatmap.GetRandomBeatmapSet();

        return await MockBeatmapSetCache(beatmapSet);
    }

    public async Task<BeatmapSet> MockBeatmapSetCache(BeatmapSet beatmapSet)
    {
        ThrowIfCacheDisabled();

        using var scope = ServicesProviderHolder.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseService>();
        await database.Beatmaps.SetCachedBeatmapSet(beatmapSet);

        return beatmapSet;
    }

    public int GetBeatmapIdFromHash(string hash)
    {
        return BeatmapIdsByHashes?.GetValueOrDefault(hash) ?? 0;
    }

    private static void ThrowIfCacheDisabled()
    {
        if (!Configuration.UseCache)
        {
            throw new InvalidOperationException("Cache is disabled; Mocking Redis is not possible.");
        }
    }
}