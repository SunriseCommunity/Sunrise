using System.Net;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sunrise.Shared.Database;
using Sunrise.Shared.Database.Extensions;
using Sunrise.Shared.Database.Models.Beatmap;
using Sunrise.Shared.Database.Objects;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Extensions;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Objects.Sessions;

namespace Sunrise.Shared.Services;

public class BeatmapService(ILogger<BeatmapService> logger, DatabaseService database, HttpClientService client)
{
    public async Task<Result<BeatmapSet, ErrorMessage>> GetBeatmapSet(BaseSession session, int? beatmapSetId = null,
        string? beatmapHash = null, int? beatmapId = null, int? retryCount = 1, CancellationToken ct = default)
    {
        if (beatmapSetId == null && beatmapHash == null && beatmapId == null)
            return Result.Failure<BeatmapSet, ErrorMessage>(new ErrorMessage
            {
                Message = "No proper ids were specified.",
                Status = HttpStatusCode.BadRequest
            });

        var beatmapSet = await database.Beatmaps.GetCachedBeatmapSet(beatmapSetId, beatmapHash, beatmapId);
        if (beatmapSet != null) return beatmapSet;

        var beatmapSetTask = Result.Failure<BeatmapSet, ErrorMessage>(new ErrorMessage
        {
            Message = "Could not retrieve beatmap set.",
            Status = HttpStatusCode.BadRequest
        });

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, ct);

        while (retryCount > 0 && !linkedCts.IsCancellationRequested && !IsValidResult(beatmapSetTask))
        {
            retryCount--;

            if (beatmapId != null)
                beatmapSetTask = await client.SendRequest<BeatmapSet>(session, ApiType.BeatmapSetDataByBeatmapId, [beatmapId], ct: linkedCts.Token);

            if (beatmapHash != null && !IsValidResult(beatmapSetTask))
                beatmapSetTask = await client.SendRequest<BeatmapSet>(session, ApiType.BeatmapSetDataByHash, [beatmapHash], ct: linkedCts.Token);

            if (beatmapSetId != null && !IsValidResult(beatmapSetTask))
                beatmapSetTask = await client.SendRequest<BeatmapSet>(session, ApiType.BeatmapSetDataById, [beatmapSetId], ct: linkedCts.Token);

            if (!IsValidResult(beatmapSetTask) && !linkedCts.IsCancellationRequested)
            {
                logger.LogWarning($"Error while getting beatmap set: {beatmapSetTask.Error.Message}, Retry count: {retryCount}");

                if (retryCount > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), linkedCts.Token);
                }
            }
        }

        if (beatmapSetTask.IsFailure)
            return beatmapSetTask;

        beatmapSet = beatmapSetTask.Value;

        await database.Beatmaps.SetCachedBeatmapSet(beatmapSet);

        var customStatuses = await database.Beatmaps.CustomStatuses.GetCustomBeatmapSetStatuses(beatmapSet.Id,
            new QueryOptions(true)
            {
                QueryModifier = q => q.Cast<CustomBeatmapStatus>().IncludeBeatmapNominator()
            },
            linkedCts.Token);

        beatmapSet.UpdateBeatmapRanking(customStatuses);

        return beatmapSet;
    }

    public async Task<Result<List<BeatmapSet>, ErrorMessage>> SearchBeatmapSets(BaseSession session, string? rankedStatus, string mode,
        string query, Pagination pagination, CancellationToken ct = default)
    {
        var beatmapSetsResult = await client.SendRequest<List<BeatmapSet>>(session,
            ApiType.BeatmapSetSearch,
            [query, pagination.PageSize, pagination.Page * pagination.PageSize, rankedStatus, mode],
            ct: ct);

        if (beatmapSetsResult.IsFailure)
            return beatmapSetsResult;

        var beatmapSets = beatmapSetsResult.Value;

        if (beatmapSets == null) return new List<BeatmapSet>();

        foreach (var set in beatmapSets)
        {
            var customStatuses = await database.Beatmaps.CustomStatuses.GetCustomBeatmapSetStatuses(set.Id,
                new QueryOptions(true)
                {
                    QueryModifier = q => q.Cast<CustomBeatmapStatus>().Include(x => x.UpdatedByUser)
                },
                ct);

            set.UpdateBeatmapRanking(customStatuses);
        }

        return beatmapSets;
    }

    private bool IsValidResult(Result<BeatmapSet, ErrorMessage> result)
    {
        var isNotFoundResult = result.IsFailure && result.Error.Status == HttpStatusCode.NotFound;
        var isValidResult = result.IsSuccess || isNotFoundResult;

        return isValidResult;
    }
}