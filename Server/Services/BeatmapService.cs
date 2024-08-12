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

    public static async Task<BeatmapSet?> GetBeatmapSet(int? beatmapId = null, int? beatmapSetId = null, string? beatmapHash = null)
    {
        if (beatmapId == null && beatmapSetId == null && beatmapHash == null)
        {
            return null;
        }

        var redis = ServicesProviderHolder.ServiceProvider.GetRequiredService<RedisRepository>();

        BeatmapSet? beatmapSet = null;
        if (beatmapId != null) beatmapSet = await redis.Get<BeatmapSet?>(string.Format(RedisKey.BeatmapSetByBeatmapId, beatmapId));
        if (beatmapSetId != null) beatmapSet = await redis.Get<BeatmapSet?>(string.Format(RedisKey.BeatmapSetBySetId, beatmapSetId));
        if (beatmapHash != null) beatmapSet = await redis.Get<BeatmapSet?>(string.Format(RedisKey.BeatmapSetByHash, beatmapHash));

        if (beatmapSet != null)
        {
            return beatmapSet;
        }

        if (beatmapId != null) beatmapSet = await RequestsHelper.SendRequest<BeatmapSet>($"{Api}v2/b/{beatmapId}?full=true");
        if (beatmapSetId != null) beatmapSet = await RequestsHelper.SendRequest<BeatmapSet>($"{Api}v2/s/{beatmapSetId}");
        if (beatmapHash != null) beatmapSet = await RequestsHelper.SendRequest<BeatmapSet>($"{Api}v2/md5/{beatmapHash}?full=true");

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

        // NOTE: Not happy with this robust key system, please refactor if possible.

        if (beatmapId != null) await redis.Set(string.Format(RedisKey.BeatmapSetByBeatmapId, beatmapId), beatmapSet);
        if (beatmapSetId != null) await redis.Set(string.Format(RedisKey.BeatmapSetBySetId, beatmapSetId), beatmapSet);
        if (beatmapHash != null) await redis.Set(string.Format(RedisKey.BeatmapSetByHash, beatmapHash), beatmapSet);

        return beatmapSet;
    }

    public static async Task<byte[]?> GetBeatmapFile(int beatmapId)
    {
        var redis = ServicesProviderHolder.ServiceProvider.GetRequiredService<RedisRepository>();

        byte[]? beatmapFile = null;
        beatmapFile = await redis.Get<byte[]>(string.Format(RedisKey.BeatmapFile, beatmapId));

        if (beatmapFile != null)
        {
            return beatmapFile;
        }

        beatmapFile = await RequestsHelper.SendRequest<byte[]>(BeatmapMirror + beatmapId);

        if (beatmapFile == null)
        {
            return null;
        }

        await redis.Set(string.Format(RedisKey.BeatmapFile, beatmapId), beatmapFile, TimeSpan.FromMinutes(60));

        return beatmapFile;
    }

    [Obsolete("Doesn't work?")]
    public static async Task<byte[]?> GetBeatmapFile(string fileName)
    {
        return await RequestsHelper.SendRequest<byte[]>(BeatmapMirror + fileName);
    }
}