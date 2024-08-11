using Sunrise.Server.Helpers;
using Sunrise.Server.Objects.Serializable;
using Sunrise.Server.Repositories;
using Sunrise.Server.Types.Enums;
using Sunrise.Server.Utils;

namespace Sunrise.Server.Services;

public static class BeatmapService
{
    private const string Api = "https://osu.direct/api/";
    private const string BeatmapMirror = "https://old.ppy.sh/osu/";

    public static async Task<Beatmap?> GetBeatmap(string hash)
    {
        var redis = ServicesProviderHolder.ServiceProvider.GetRequiredService<RedisRepository>();

        var cachedScores = await redis.Get<Beatmap?>(string.Format(RedisKey.BeatmapHash, hash));

        if (cachedScores != null)
        {
            return cachedScores;
        }

        var beatmap = await RequestsHelper.SendRequest<Beatmap>($"{Api}v2/md5/{hash}");

        if (beatmap == null)
        {
            return null;
        }

        if (Configuration.IgnoreBeatmapRanking)
        {
            beatmap.StatusString = "ranked";
        }

        await redis.Set(string.Format(RedisKey.BeatmapHash, hash), beatmap, TimeSpan.FromMinutes(15));

        return beatmap;
    }

    public static async Task<BeatmapSet?> GetBeatmapSet(int beatmapId = -1, int beatmapSetId = -1)
    {
        if (beatmapId == -1 && beatmapSetId == -1)
        {
            return null;
        }

        var redis = ServicesProviderHolder.ServiceProvider.GetRequiredService<RedisRepository>();

        var cachedScores = beatmapId != -1
            ? await redis.Get<BeatmapSet?>(string.Format(RedisKey.BeatmapSet, beatmapId))
            : await redis.Get<BeatmapSet?>(string.Format(RedisKey.BeatmapSetBySet, beatmapSetId));

        if (cachedScores != null)
        {
            return cachedScores;
        }

        var beatmapSet = await RequestsHelper.SendRequest<BeatmapSet>(beatmapId != -1
            ? $"{Api}v2/b/{beatmapId}?full=true"
            : $"{Api}v2/s/{beatmapSetId}");

        if (beatmapSet == null)
        {
            return null;
        }

        if (Configuration.IgnoreBeatmapRanking)
        {
            foreach (var t in beatmapSet.Beatmaps)
            {
                t.StatusString = "ranked";
            }
        }

        await redis.Set(beatmapId != -1
                ? string.Format(RedisKey.BeatmapSet, beatmapId)
                : string.Format(RedisKey.BeatmapSetBySet, beatmapSetId),
            beatmapSet);

        return beatmapSet;
    }

    public static async Task<byte[]?> GetBeatmapFileBy(int id)
    {
        var redis = ServicesProviderHolder.ServiceProvider.GetRequiredService<RedisRepository>();

        var cachedBeatmap = await redis.Get<byte[]>(string.Format(RedisKey.BeatmapFile, id));

        if (cachedBeatmap != null)
        {
            return cachedBeatmap;
        }

        var beatmap = await RequestsHelper.SendRequest<byte[]>(BeatmapMirror + id);

        if (beatmap == null)
        {
            return null;
        }

        await redis.Set(string.Format(RedisKey.BeatmapFile, id), beatmap, TimeSpan.FromMinutes(60));

        return beatmap;
    }

    public static async Task<byte[]?> GetBeatmapFileBy(string fileName)
    {
        return await RequestsHelper.SendRequest<byte[]>(BeatmapMirror + fileName);
    }
}