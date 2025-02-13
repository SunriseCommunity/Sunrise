using Sunrise.Server.Application;
using Sunrise.Server.Database;
using Sunrise.Server.Extensions;
using Sunrise.Server.Helpers;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.Serializable;
using Sunrise.Server.Types.Enums;

namespace Sunrise.Server.Managers;

public static class BeatmapManager
{
    public static async Task<BeatmapSet?> GetBeatmapSet(BaseSession session, int? beatmapSetId = null,
        string? beatmapHash = null, int? beatmapId = null)
    {
        if (beatmapSetId == null && beatmapHash == null && beatmapId == null) return null;

        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();

        var beatmapSet = await database.BeatmapService.GetCachedBeatmapSet(beatmapSetId, beatmapHash, beatmapId);
        if (beatmapSet != null) return beatmapSet;

        if (beatmapId != null)
            beatmapSet =
                await RequestsHelper.SendRequest<BeatmapSet>(session, ApiType.BeatmapSetDataByBeatmapId, [beatmapId]);
        if (beatmapHash != null && beatmapSet == null)
            beatmapSet =
                await RequestsHelper.SendRequest<BeatmapSet>(session, ApiType.BeatmapSetDataByHash, [beatmapHash]);
        if (beatmapSetId != null && beatmapSet == null)
            beatmapSet =
                await RequestsHelper.SendRequest<BeatmapSet>(session, ApiType.BeatmapSetDataById, [beatmapSetId]);

        if (beatmapSet == null)
            // TODO: Save null to cache if it requested map not found.
            return null;

        beatmapSet.UpdateBeatmapRanking();

        await database.BeatmapService.SetCachedBeatmapSet(beatmapSet);

        return beatmapSet;
    }

    public static async Task<byte[]?> GetBeatmapFile(BaseSession session, int beatmapId)
    {
        var database = ServicesProviderHolder.GetRequiredService<DatabaseManager>();
        var beatmapFile = await database.BeatmapService.Files.GetBeatmapFile(beatmapId);

        if (beatmapFile != null) return beatmapFile;

        beatmapFile = await RequestsHelper.SendRequest<byte[]>(session, ApiType.BeatmapDownload, [beatmapId]);

        if (beatmapFile == null) return null;

        await database.BeatmapService.Files.SetBeatmapFile(beatmapId, beatmapFile);
        return beatmapFile;
    }
}