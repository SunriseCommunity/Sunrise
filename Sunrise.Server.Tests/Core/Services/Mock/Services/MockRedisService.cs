using Microsoft.Extensions.Configuration;
using Sunrise.Shared.Application;
using Sunrise.Shared.Database;
using Sunrise.Shared.Objects.Serializable;

namespace Sunrise.Server.Tests.Core.Services.Mock.Services;

public class MockRedisService(MockService service)
{

    private readonly FileService _fileService = new();
    private static Dictionary<int, string>? BeatmapFileNamesById => Configuration.GetConfig().GetSection("Test:BeatmapFileNamesById").Get<Dictionary<int, string>>();
    private static Dictionary<string, string>? BeatmapFileNamesByHash => Configuration.GetConfig().GetSection("Test:BeatmapFileNamesByHash").Get<Dictionary<string, string>>();

    public async Task<BeatmapSet> MockBeatmapSetCache()
    {
        ThrowIfCacheDisabled();

        var beatmapSet = service.Beatmap.GetRandomBeatmapSet();

        return await MockBeatmapSetCache(beatmapSet);
    }

    public async Task<BeatmapSet> MockBeatmapSetCache(BeatmapSet beatmapSet)
    {
        ThrowIfCacheDisabled();

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        await database.BeatmapService.SetCachedBeatmapSet(beatmapSet);

        return beatmapSet;
    }

    public async Task<int> MockLocalBeatmapFile(string beatmapHash)
    {
        ThrowIfCacheDisabled();

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var beatmapFileName = BeatmapFileNamesByHash?.GetValueOrDefault(beatmapHash);

        if (beatmapFileName == null)
        {
            throw new InvalidOperationException($"Beatmap file name for beatmap hash {beatmapHash} not found.");
        }

        var beatmapFilePath = _fileService.GetFileByName(beatmapFileName);

        if (beatmapFilePath == null)
        {
            throw new InvalidOperationException($"Beatmap file for beatmap hash {beatmapHash} not found.");
        }

        var beatmap = await File.ReadAllBytesAsync(beatmapFilePath);

        if (beatmap == null)
        {
            throw new InvalidOperationException($"Beatmap file for beatmap hash {beatmapHash} not found.");
        }

        var beatmapId = BeatmapFileNamesById?
            .FirstOrDefault(x => x.Value == beatmapFileName).Key;

        if (beatmapId == null)
        {
            throw new InvalidOperationException($"Beatmap ID for beatmap hash {beatmapHash} not found.");
        }

        await database.BeatmapService.Files.SetBeatmapFile(beatmapId.Value, beatmap);

        return beatmapId.Value;
    }

    public async Task MockBeatmapFile(int beatmapSetId)
    {
        ThrowIfCacheDisabled();

        await MockBeatmapFile(beatmapSetId, []);
    }

    public async Task MockBeatmapFile(int beatmapSetId, byte[] beatmap)
    {
        ThrowIfCacheDisabled();

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        await database.BeatmapService.Files.SetBeatmapFile(beatmapSetId, beatmap);
    }

    private static void ThrowIfCacheDisabled()
    {
        if (!Configuration.UseCache)
        {
            throw new InvalidOperationException("Cache is disabled; Mocking Redis is not possible.");
        }
    }
}