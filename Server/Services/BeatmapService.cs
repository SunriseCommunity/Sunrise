using System.Text.Json;
using Sunrise.Server.Objects.Serializable;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Services;

public class BeatmapService(ServicesProvider services)
{
    private const string Api = "https://osu.direct/api/";
    private const string BeatmapMirror = "https://old.ppy.sh/osu/";

    private static readonly HttpClient Client = new();

    public async Task<Beatmap?> GetBeatmap(string hash)
    {
        var cachedScores = await services.Redis.Get<Beatmap?>(string.Format(RedisKey.BeatmapHash, hash));

        if (cachedScores != null)
        {
            return cachedScores;
        }

        var response = await Client.GetAsync($"{Api}v2/md5/{hash}");

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Beatmap not found or rate limited");
        }

        var content = await response.Content.ReadAsStringAsync();

        if (string.IsNullOrEmpty(content))
        {
            return null;
        }

        var beatmap = JsonSerializer.Deserialize<Beatmap>(content);

        if (Configuration.IgnoreBeatmapRanking && beatmap != null)
        {
            beatmap.StatusString = "ranked";
        }

        if (beatmap != null)
        {
            await services.Redis.Set(string.Format(RedisKey.BeatmapHash, hash), beatmap, TimeSpan.FromMinutes(15));
        }

        return beatmap;
    }

    public async Task<Beatmap?> GetBeatmapById(int id)
    {
        var cachedScores = await services.Redis.Get<Beatmap?>(string.Format(RedisKey.Beatmap, id));

        if (cachedScores != null)
        {
            return cachedScores;
        }

        var response = await Client.GetAsync($"{Api}v2/b/{id}");

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Beatmap not found or rate limited");
        }

        var content = await response.Content.ReadAsStringAsync();

        if (string.IsNullOrEmpty(content))
        {
            return null;
        }

        var beatmap = JsonSerializer.Deserialize<Beatmap>(content);

        if (Configuration.IgnoreBeatmapRanking && beatmap != null)
        {
            beatmap.StatusString = "ranked";
        }

        if (beatmap != null)
        {
            await services.Redis.Set(string.Format(RedisKey.Beatmap, id), beatmap, TimeSpan.FromMinutes(15));
        }

        return beatmap;
    }

    public async Task<byte[]?> GetBeatmapFileById(int id)
    {
        var cachedBeatmap = await services.Redis.Get<byte[]>(string.Format(RedisKey.BeatmapFile, id));

        if (cachedBeatmap != null)
        {
            return cachedBeatmap;
        }

        var response = await Client.GetAsync($"{BeatmapMirror}{id}");

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Beatmap not found or rate limited");
        }

        var beatmap = await response.Content.ReadAsByteArrayAsync();

        await services.Redis.Set(string.Format(RedisKey.BeatmapFile, id), beatmap, TimeSpan.FromMinutes(60));

        return beatmap;
    }
}